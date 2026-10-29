using EmployeeLeaveApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Data;

public static class DbSeeder
{
    /// <summary>Idempotently adds Employees.PhotoUrl to existing databases (EnsureCreated won't alter tables).</summary>
    private static void EnsurePhotoUrlColumn(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            var exists = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info('Employees');";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    if (string.Equals(r.GetString(1), "PhotoUrl", StringComparison.OrdinalIgnoreCase)) exists = true;
            }
            if (!exists)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Employees ADD COLUMN PhotoUrl TEXT NULL;";
                alter.ExecuteNonQuery();
            }
        }
        finally { if (!wasOpen) conn.Close(); }
    }

    /// <summary>Idempotently adds Users.ResetCode / ResetCodeExpiry (forgot-password) to existing databases.</summary>
    private static void EnsureResetColumns(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info('Users');";
                using var r = cmd.ExecuteReader();
                while (r.Read()) cols.Add(r.GetString(1));
            }
            foreach (var (name, type) in new[] { ("ResetCode", "TEXT"), ("ResetCodeExpiry", "TEXT") })
            {
                if (cols.Contains(name)) continue;
                using var alter = conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE Users ADD COLUMN {name} {type} NULL;";
                alter.ExecuteNonQuery();
            }
        }
        finally { if (!wasOpen) conn.Close(); }
    }

    public static void Seed(AppDbContext db, bool demoMode = false)
    {
        db.Database.EnsureCreated();
        EnsurePhotoUrlColumn(db);
        EnsureResetColumns(db);
        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                FullName = "System Admin",
                Email = "admin@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        if (!db.Departments.Any())
        {
            db.Departments.AddRange(
                new Department { Name = "HR", Description = "Human Resources" },
                new Department { Name = "Engineering", Description = "Software development and IT" },
                new Department { Name = "Finance", Description = "Accounts and payroll" },
                new Department { Name = "Sales", Description = "Sales and business development" });
            db.SaveChanges();
        }

        if (!db.Employees.Any())
        {
            var d = db.Departments.ToDictionary(x => x.Name, x => x.Id);
            var list = new[]
            {
                ("Ayesha", "Khan", "ayesha.khan@company.com", "0300-1234567", "HR Manager", "HR", 180000m, "2022-03-01"),
                ("Ali", "Raza", "ali.raza@company.com", "0301-2345678", "Senior Software Engineer", "Engineering", 250000m, "2021-07-15"),
                ("Fatima", "Ahmed", "fatima.ahmed@company.com", "0302-3456789", "Software Engineer", "Engineering", 150000m, "2023-01-10"),
                ("Usman", "Tariq", "usman.tariq@company.com", "0303-4567890", "Accountant", "Finance", 120000m, "2022-09-05"),
                ("Hina", "Malik", "hina.malik@company.com", "0304-5678901", "Sales Executive", "Sales", 95000m, "2024-02-20"),
                ("Bilal", "Hussain", "bilal.hussain@company.com", "0305-6789012", "Sales Manager", "Sales", 200000m, "2020-11-01")
            };
            var i = 1;
            foreach (var (fn, ln, em, ph, des, dep, sal, join) in list)
                db.Employees.Add(new Employee
                {
                    EmployeeCode = $"EMP{i++:D4}", FirstName = fn, LastName = ln, Email = em, Phone = ph,
                    Designation = des, DepartmentId = d[dep], BasicSalary = sal, JoiningDate = DateTime.Parse(join),
                    Status = EmployeeStatus.Active
                });
            db.SaveChanges();
        }

        // Public demo only: ~100 employees, demo logins, a month of attendance, leaves and payslips.
        if (demoMode) DemoSeeder.Seed(db);

        SeedEmployeeUsers(db);
        SeedPayslips(db);

        var emps = db.Employees.OrderBy(e => e.Id).ToList();
        var today = DateTime.Today;

        if (!db.LeaveRequests.Any() && emps.Count >= 4)
        {
            LeaveRequest L(Employee e, LeaveType t, int fromOffset, int days, string reason, LeaveStatus s, string? remarks = null) => new()
            {
                EmployeeId = e.Id, LeaveType = t, StartDate = today.AddDays(fromOffset),
                EndDate = today.AddDays(fromOffset + days - 1), Days = days, Reason = reason, Status = s, AdminRemarks = remarks
            };
            db.LeaveRequests.AddRange(
                L(emps[1], LeaveType.Annual, 7, 3, "Family trip", LeaveStatus.Pending),
                L(emps[2], LeaveType.Sick, -10, 2, "Fever", LeaveStatus.Approved, "Get well soon"),
                L(emps[3], LeaveType.Casual, 3, 1, "Personal errand", LeaveStatus.Pending),
                L(emps[4], LeaveType.Unpaid, -20, 2, "Extended personal leave", LeaveStatus.Rejected, "Peak sales period"));
            db.SaveChanges();
        }

        if (!db.Attendances.Any() && emps.Count > 0)
        {
            var statuses = new[] { AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Late,
                AttendanceStatus.Present, AttendanceStatus.HalfDay, AttendanceStatus.Absent };
            for (var back = 4; back >= 0; back--)
            {
                var day = today.AddDays(-back);
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                for (var k = 0; k < emps.Count; k++)
                {
                    var s = statuses[(k + back) % statuses.Length];
                    db.Attendances.Add(new Attendance
                    {
                        EmployeeId = emps[k].Id, Date = day, Status = s,
                        CheckIn = s == AttendanceStatus.Absent ? null : s == AttendanceStatus.Late ? new TimeSpan(10, 15, 0) : new TimeSpan(9, 0, 0),
                        CheckOut = s == AttendanceStatus.Absent ? null : s == AttendanceStatus.HalfDay ? new TimeSpan(13, 0, 0) : new TimeSpan(18, 0, 0),
                        Notes = s == AttendanceStatus.Late ? "Traffic" : null
                    });
                }
            }
            db.SaveChanges();
        }
    }

    /// <summary>Give every seeded employee a login (Employee@123), idempotently.</summary>
    private static readonly string[] SeededEmails =
    {
        "ayesha.khan@company.com", "ali.raza@company.com", "fatima.ahmed@company.com",
        "usman.tariq@company.com", "hina.malik@company.com", "bilal.hussain@company.com"
    };

    private static void SeedEmployeeUsers(AppDbContext db)
    {
        foreach (var e in db.Employees.Where(x => SeededEmails.Contains(x.Email)).ToList())
        {
            var user = db.Users.FirstOrDefault(u => u.Email == e.Email);
            if (user == null)
            {
                user = new User
                {
                    FullName = e.FirstName + " " + e.LastName, Email = e.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
                    Role = "Employee", CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(user);
                db.SaveChanges();
            }
            if (e.UserId == null && !db.Employees.Any(x => x.UserId == user.Id))
            {
                e.UserId = user.Id;
                db.SaveChanges();
            }
        }
    }

    /// <summary>Previous month (Paid) and current month (Draft) payslips for seeded employees, idempotently.</summary>
    private static void SeedPayslips(AppDbContext db)
    {
        var now = DateTime.Today;
        var prev = now.AddMonths(-1);
        var emps = db.Employees.Where(x => SeededEmails.Contains(x.Email)).ToList();
        foreach (var (m, y, paid) in new[] { (prev.Month, prev.Year, true), (now.Month, now.Year, false) })
        foreach (var e in emps)
        {
            if (db.Payslips.Any(p => p.EmployeeId == e.Id && p.Month == m && p.Year == y)) continue;
            var p = new Payslip
            {
                EmployeeId = e.Id, Month = m, Year = y, BasicSalary = e.BasicSalary,
                Allowances = Math.Round(e.BasicSalary * 0.10m), Deductions = 0,
                Tax = Math.Round(e.BasicSalary * 0.05m),
                Status = paid ? PayslipStatus.Paid : PayslipStatus.Draft, GeneratedAt = DateTime.UtcNow,
                PaidAt = paid ? new DateTime(y, m, DateTime.DaysInMonth(y, m)) : null
            };
            p.Recalculate();
            db.Payslips.Add(p);
        }
        db.SaveChanges();
    }
}
