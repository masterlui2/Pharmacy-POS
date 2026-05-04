# PharmacyPOS

ASP.NET Core MVC pharmacy system targeting `.NET 10`.

## Deploying to Render

This repo is configured for Docker-based deployment on Render.

### Important constraints

- The app uses `Microsoft.EntityFrameworkCore.SqlServer`, so it needs an external SQL Server-compatible database.
- Render docs currently list managed Postgres and Key Value services, not a managed SQL Server offering. In practice, this means you should use an external SQL Server or Azure SQL connection string for `ConnectionStrings__DefaultConnection`.
- The app writes audit logs and pharmacist messages under `App_Data/`. On Render, attach a persistent disk at `/app/App_Data` if you want those files to survive redeploys and restarts.

### Files added for deployment

- `Dockerfile`
- `.dockerignore`
- `appsettings.Production.json`

### Render service settings

- Runtime: `Docker`
- Dockerfile path: `./Dockerfile`
- Health check path: `/healthz`
- Disk mount path: `/app/App_Data` if you need persistent local file storage

### Required environment variables

Set these in Render before the first successful production deploy:

```text
ConnectionStrings__DefaultConnection=<your SQL Server connection string>
Firebase__ProjectId=<your Firebase project id>
Firebase__ServiceAccountPath=/etc/secrets/firebase-service-account.json
GoogleRecaptcha__SiteKey=<your recaptcha site key>
GoogleRecaptcha__SecretKey=<your recaptcha secret key>
GoogleMapsDelivery__ApiKey=<your Google Maps API key>
PayMongo__Enabled=true
PayMongo__PublicKey=<your PayMongo public key>
PayMongo__SecretKey=<your PayMongo secret key>
PayMongo__SuccessUrl=https://<your-render-domain>/Orders
PayMongo__CancelUrl=https://<your-render-domain>/Orders
```

### Firebase secret file on Render

Upload your Firebase service account JSON as a Render secret file named:

```text
firebase-service-account.json
```

Render mounts Docker secret files at:

```text
/etc/secrets/<filename>
```

So the matching environment variable should be:

```text
Firebase__ServiceAccountPath=/etc/secrets/firebase-service-account.json
```

### Notes

- `appsettings.Production.json` intentionally clears local-machine settings so production does not try to use your local `SQLEXPRESS` instance or `C:\secure\...` paths.
- The container listens on port `10000`, which matches Render's default web service port.
- The app now exposes a health endpoint at `/healthz`.
