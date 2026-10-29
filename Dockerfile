# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EmployeeLeaveApi.csproj ./
RUN dotnet restore EmployeeLeaveApi.csproj
COPY . .
RUN dotnet publish EmployeeLeaveApi.csproj -c Release -o /out /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# Writable folders for the SQLite database and uploaded photos (image runs as non-root user "app")
RUN mkdir -p /app/data /app/wwwroot/uploads/avatars && chown -R app:app /app/data /app/wwwroot
USER app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/employeeleave.db"
EXPOSE 8080
# Render injects $PORT; default to 8080 elsewhere
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet EmployeeLeaveApi.dll"]
