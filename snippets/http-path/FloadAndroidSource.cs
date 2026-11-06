// The Android half of the HTTP path: Google Play's Install Referrer and the app facts.
// Works in classic Xamarin.Android and in .NET for Android, with the
// Xamarin.Google.Android.InstallReferrer package. MIT licensed.
#if __ANDROID__
using System;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Xamarin.Android.InstallReferrer.Api;   // the namespace in Xamarin.Google.Android.InstallReferrer 2.2.x

namespace Fload.HttpAttribution
{
    public static class FloadAndroid
    {
        static FloadHttpAttribution instance;

        /// <summary>
        /// Call once in Application.OnCreate (or your launcher activity's OnCreate). Reports the
        /// install on the first launch and sends what is queued. Call Flush in OnResume.
        /// </summary>
        public static FloadHttpAttribution Start(Context context, string writeKey, Uri endpoint = null)
        {
            if (instance != null) return instance;
            var app = context.ApplicationContext ?? context;
            // NoBackupFilesDir is never backed up: a restored phone is a new install.
            instance = new FloadHttpAttribution(writeKey, app.NoBackupFilesDir.AbsolutePath, endpoint);
            var started = instance.StartAsync(Facts(app), () => ReadReferrerAsync(app));
            return instance;
        }

        /// <summary>Sends what is queued. Call it in your activity's OnResume.</summary>
        public static Task Flush()
        {
            return instance == null ? Task.CompletedTask : instance.FlushAsync();
        }

        /// <summary>Links your app user id (e.g. RevenueCat's AppUserID).</summary>
        public static Task SetUserIdAsync(string userId)
        {
            return instance == null ? Task.CompletedTask : instance.SetUserIdAsync(userId);
        }

        static FloadAppFacts Facts(Context context)
        {
            var package = context.PackageManager.GetPackageInfo(context.PackageName, 0);
            var build = Build.VERSION.SdkInt >= BuildVersionCodes.P ? package.LongVersionCode : package.VersionCode;
            return new FloadAppFacts
            {
                Platform = "android",
                AppId = context.PackageName,
                AppVersion = package.VersionName,
                AppBuild = build.ToString(System.Globalization.CultureInfo.InvariantCulture),
                OsVersion = Build.VERSION.Release,
                DeviceModel = Build.Manufacturer + " " + Build.Model,
                Locale = Java.Util.Locale.Default.ToLanguageTag(),
            };
        }

        // Google: read the referrer once, on the first run after install, then close the connection.
        static async Task<FloadInstallSource> ReadReferrerAsync(Context context)
        {
            var done = new TaskCompletionSource<FloadInstallSource>();
            InstallReferrerClient client;
            try
            {
                client = InstallReferrerClient.NewBuilder(context).Build();
                client.StartConnection(new Listener(client, done));
            }
            catch (Exception)
            {
                return new FloadInstallSource { Status = "error" };
            }
            var finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            if (finished != done.Task)
            {
                client.EndConnection();
                return new FloadInstallSource { Status = "unavailable" };
            }
            return done.Task.Result;
        }

        sealed class Listener : Java.Lang.Object, IInstallReferrerStateListener
        {
            readonly InstallReferrerClient client;
            readonly TaskCompletionSource<FloadInstallSource> done;

            public Listener(InstallReferrerClient client, TaskCompletionSource<FloadInstallSource> done)
            {
                this.client = client;
                this.done = done;
            }

            public void OnInstallReferrerSetupFinished(int responseCode)
            {
                FloadInstallSource source;
                try
                {
                    source = Read(responseCode);
                }
                catch (Exception)
                {
                    source = new FloadInstallSource { Status = "error" };
                }
                client.EndConnection();
                done.TrySetResult(source);
            }

            public void OnInstallReferrerServiceDisconnected()
            {
                done.TrySetResult(new FloadInstallSource { Status = "unavailable" });
            }

            // InstallReferrerClient.InstallReferrerResponse, as Google documents them.
            const int Ok = 0;
            const int ServiceUnavailable = 1;
            const int FeatureNotSupported = 2;

            FloadInstallSource Read(int responseCode)
            {
                switch (responseCode)
                {
                    case Ok:
                        var details = client.InstallReferrer;
                        return new FloadInstallSource
                        {
                            Status = "ok",
                            PlayReferrer = details.InstallReferrer,
                            PlayReferrerClickAt = FromSeconds(details.ReferrerClickTimestampSeconds),
                            PlayInstallBeginAt = FromSeconds(details.InstallBeginTimestampSeconds),
                            PlayInstant = details.GooglePlayInstantParam,
                        };
                    case FeatureNotSupported:
                        return new FloadInstallSource { Status = "not_supported" };
                    case ServiceUnavailable:
                        return new FloadInstallSource { Status = "unavailable" };
                    default:
                        return new FloadInstallSource { Status = "error" };
                }
            }

            static DateTimeOffset? FromSeconds(long seconds)
            {
                return seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
            }
        }
    }
}
#endif
