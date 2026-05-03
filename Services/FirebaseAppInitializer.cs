using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public sealed class FirebaseAppInitializer
{
    private static readonly object SyncRoot = new();
    private static FirebaseApp? defaultApp;

    public FirebaseAppInitializer(
        IOptions<FirebaseOptions> optionsAccessor,
        ILogger<FirebaseAppInitializer> logger)
    {
        var options = optionsAccessor.Value;
        if (!TryResolveConfiguration(options, out var projectId, out var serviceAccountPath, out var reason))
        {
            AuthenticationUnavailableReason = reason;
            FirestoreUnavailableReason = reason;
            logger.LogWarning(
                "Firebase integration is disabled. {Reason}",
                reason);
            return;
        }

        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", serviceAccountPath);
            App = EnsureInitialized(serviceAccountPath);
        }
        catch (Exception exception)
        {
            AuthenticationUnavailableReason =
                $"Firebase authentication initialization failed: {exception.Message}";
            FirestoreUnavailableReason = AuthenticationUnavailableReason;
            logger.LogError(
                exception,
                "Firebase authentication is disabled because initialization failed.");
            return;
        }

        try
        {
            Firestore = FirestoreDb.Create(projectId);
        }
        catch (Exception exception)
        {
            FirestoreUnavailableReason =
                $"Cloud Firestore initialization failed: {exception.Message}";
            logger.LogError(
                exception,
                "Cloud Firestore integration is disabled because initialization failed.");
        }
    }

    public FirebaseApp? App { get; }

    public FirestoreDb? Firestore { get; }

    public bool IsAuthenticationAvailable => App is not null;

    public bool IsFirestoreAvailable => Firestore is not null;

    public bool IsAvailable => IsAuthenticationAvailable && IsFirestoreAvailable;

    public string? AuthenticationUnavailableReason { get; }

    public string? FirestoreUnavailableReason { get; }

    public string? UnavailableReason =>
        FirestoreUnavailableReason ?? AuthenticationUnavailableReason;

    private static FirebaseApp EnsureInitialized(string serviceAccountPath)
    {
        lock (SyncRoot)
        {
            if (defaultApp is not null)
            {
                return defaultApp;
            }

            try
            {
                defaultApp = FirebaseApp.Create(new AppOptions
                {
                    Credential = CredentialFactory
                        .FromFile<ServiceAccountCredential>(serviceAccountPath)
                        .ToGoogleCredential()
                });
            }
            catch (InvalidOperationException)
            {
                defaultApp = FirebaseApp.DefaultInstance;
            }

            if (defaultApp is null)
            {
                throw new InvalidOperationException(
                    "FirebaseApp initialization did not return an app instance.");
            }

            return defaultApp;
        }
    }

    private static bool TryResolveConfiguration(
        FirebaseOptions options,
        out string projectId,
        out string serviceAccountPath,
        out string reason)
    {
        projectId = options.ProjectId.Trim();
        serviceAccountPath = options.ServiceAccountPath.Trim();

        if (string.IsNullOrWhiteSpace(projectId))
        {
            reason = "Set Firebase:ProjectId before using Firebase-backed features.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceAccountPath))
        {
            reason = "Set Firebase:ServiceAccountPath before using Firebase-backed features.";
            return false;
        }

        if (!File.Exists(serviceAccountPath))
        {
            reason = $"The Firebase service account file was not found at '{serviceAccountPath}'.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
