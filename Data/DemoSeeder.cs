using EmployeeLeaveApi.Models;

namespace EmployeeLeaveApi.Data;

/// <summary>
/// Seeds a realistic dataset for the public live demo (only runs when DemoMode=true).
/// Idempotent: safe to call on every start-up.
/// </summary>
public static class DemoSeeder
{
    public const string AdminEmail = "demo.admin@hrportal.demo";
    public const string EmployeeEmail = "demo.employee@hrportal.demo";
    public const string Password = "Demo@123";
    private const int TargetEmployees = 100;

    private static readonly string[] FirstNames =
    {
        "Ahmed", "Sara", "Hamza", "Zara", "Omar", "Maryam", "Hassan", "Iqra", "Imran", "Sana",
        "Kamran", "Nida", "Faisal", "Rabia", "Junaid", "Amna", "Saad", "Mehwish", "Waqas", "Sadia",
        "Adeel", "Kiran", "Asad", "Anam", "Talha", "Hira", "Shoaib", "Laiba", "Danish", "Areeba"
    };

    private static readonly string[] LastNames =
    {
        "Khan", "Ahmed", "Malik", "Hussain", "Raza", "Iqbal", "Shah", "Butt", "Chaudhry", "Qureshi",
        "Siddiqui", "Sheikh", "Abbasi", "Mirza", "Javed"
    };

    private static readonly (string Dept, string[] Titles, int Min, int Max)[] Roles =
    {
        ("HR", new[] { "HR Executive", "Recruiter", "HR Officer" }, 80000, 160000),
        ("Engineering", new[] { "Software Engineer", "Frontend Developer", "Backend Developer", "QA Engineer", "DevOps Engineer" }, 120000, 300000),
        ("Finance", new[] { "Accountant", "Finance Officer", "Payroll Specialist" }, 90000, 180000),
        ("Sales", new[] { "Sales Executive", "Account Manager", "Business Developer" }, 70000, 170000)
    };

    public static void Seed(AppDbContext db)
    {
        var rnd = new Random(2024);
        var depts = db.Departments.ToDictionary(d => d.Name, d => d.Id);
        int Dept(string name) => depts.TryGetValue(name, out var id) ? id : depts.Values.First();
        var today = DateTime.Today;

        // 1) Demo logins
        if (!db.Users.Any(u => u.Email == AdminEmail))
        {
            db.Users.Add(new User
            {
                FullName = "Demo Admin", Email = AdminEmail, Role = "Admin", CreatedAt = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
            });
            db.SaveChanges();
        }
        var demoUser = db.Users.FirstOrDefault(u => u.Email == EmployeeEmail);
        if (demoUser == null)
        {
            demoUser = new User
            {
                FullName = "Demo Employee", Email = EmployeeEmail, Role = "Employee", CreatedAt = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
            };
            db.Users.Add(demoUser);
            db.SaveChanges();
        }

        // 2) Employees up to ~100
        var count = db.Employees.Count();
        var next = count + 1;
        string Code() { string c; do { c = $"EMP{next++:D4}"; } while (db.Employees.Local.Any(e => e.EmployeeCode == c) || db.Employees.Any(e => e.EmployeeCode == c)); return c; }

        if (!db.Employees.Any(e => e.Email == EmployeeEmail))
        {
            db.Employees.Add(new Employee
            {
                EmployeeCode = Code(), FirstName = "Demo", LastName = "Employee", Email = EmployeeEmail,
                Phone = "0300-0000000", Designation = "Frontend Developer", DepartmentId = Dept("Engineering"),
                BasicSalary = 150000m, JoiningDate = today.AddYears(-1), Status = EmployeeStatus.Active, UserId = demoUser.Id
            });
            db.SaveChanges();
            count++;
        }

        if (count < TargetEmployees)
        {
            while (count < TargetEmployees)
            {
                var fn = FirstNames[rnd.Next(FirstNames.Length)];
                var ln = LastNames[rnd.Next(LastNames.Length)];
                var role = Roles[rnd.Next(Roles.Length)];
                var code = Code();
                db.Employees.Add(new Employee
                {
                    EmployeeCode = code, FirstName = fn, LastName = ln,
                    Email = $"{fn}.{ln}.{code}@company.com".ToLowerInvariant(),
                    Phone = $"03{rnd.Next(0, 5)}{rnd.Next(0, 10)}-{rnd.Next(1000000, 9999999)}",
                    Designation = role.Titles[rnd.Next(role.Titles.Length)], DepartmentId = Dept(role.Dept),
                    BasicSalary = Math.Round(rnd.Next(role.Min, role.Max) / 1000m) * 1000m,
                    JoiningDate = today.AddDays(-rnd.Next(60, 2000)),
                    Status = rnd.Next(20) == 0 ? EmployeeStatus.Inactive : EmployeeStatus.Active
                });
                count++;
            }
            db.SaveChanges();
        }

        var emps = db.Employees.OrderBy(e => e.Id).ToList();
        var active = emps.Where(e => e.Status == EmployeeStatus.Active).ToList();

        // 3) ~30 days of attendance
        if (!db.Attendances.Any())
        {
            for (var back = 30; back >= 0; back--)
            {
                var day = today.AddDays(-back);
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                foreach (var e in active)
                {
                    var roll = rnd.Next(100);
                    var s = roll < 78 ? AttendanceStatus.Present : roll < 88 ? AttendanceStatus.Late
                          : roll < 94 ? AttendanceStatus.HalfDay : AttendanceStatus.Absent;
                    var inMin = s == AttendanceStatus.Late ? 9 * 60 + 20 + rnd.Next(70) : 8 * 60 + 40 + rnd.Next(30);
                    db.Attendances.Add(new Attendance
                    {
                        EmployeeId = e.Id, Date = day, Status = s,
                        CheckIn = s == AttendanceStatus.Absent ? null : TimeSpan.FromMinutes(inMin),
                        CheckOut = s == AttendanceStatus.Absent ? null
                                 : s == AttendanceStatus.HalfDay ? TimeSpan.FromMinutes(13 * 60 + rnd.Next(30))
                                 : TimeSpan.FromMinutes(17 * 60 + 30 + rnd.Next(90)),
                        Notes = s == AttendanceStatus.Late ? "Traffic" : null
                    });
                }
            }
            db.SaveChanges();
        }

        // 4) Leave requests
        if (db.LeaveRequests.Count() < 10)
        {
            var reasons = new[] { "Family event", "Fever", "Personal errand", "Wedding", "Medical appointment", "Travel", "Child care" };
            var types = new[] { LeaveType.Annual, LeaveType.Sick, LeaveType.Casual, LeaveType.Annual, LeaveType.Unpaid };
            var demoEmp = emps.First(e => e.Email == EmployeeEmail);
            var targets = new List<Employee> { demoEmp, demoEmp };
            targets.AddRange(active.OrderBy(_ => rnd.Next()).Take(38));
            foreach (var e in targets)
            {
                var from = rnd.Next(-60, 30);
                var days = rnd.Next(1, 5);
                var status = from > 0 ? (rnd.Next(3) == 0 ? LeaveStatus.Approved : LeaveStatus.Pending)
                                      : (rnd.Next(5) == 0 ? LeaveStatus.Rejected : LeaveStatus.Approved);
                db.LeaveRequests.Add(new LeaveRequest
                {
                    EmployeeId = e.Id, LeaveType = types[rnd.Next(types.Length)],
                    StartDate = today.AddDays(from), EndDate = today.AddDays(from + days - 1), Days = days,
                    Reason = reasons[rnd.Next(reasons.Length)], Status = status,
                    AdminRemarks = status == LeaveStatus.Rejected ? "Peak workload" : status == LeaveStatus.Approved ? "Approved" : null
                });
            }
            db.SaveChanges();
        }

        // 5) Payslips: two paid months + current month draft
        if (db.Payslips.Count() < 20)
        {
            var months = new[] { (today.AddMonths(-2), true), (today.AddMonths(-1), true), (today, false) };
            foreach (var (dt, paid) in months)
            foreach (var e in active)
            {
                var p = new Payslip
                {
                    EmployeeId = e.Id, Month = dt.Month, Year = dt.Year, BasicSalary = e.BasicSalary,
                    Allowances = Math.Round(e.BasicSalary * 0.10m), Deductions = 0, Tax = Math.Round(e.BasicSalary * 0.05m),
                    Status = paid ? PayslipStatus.Paid : PayslipStatus.Draft, GeneratedAt = DateTime.UtcNow,
                    PaidAt = paid ? new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month)) : null
                };
                p.Recalculate();
                db.Payslips.Add(p);
            }
            db.SaveChanges();
        }
    }
}
