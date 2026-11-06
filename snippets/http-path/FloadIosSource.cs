// The iOS half of the HTTP path: Apple's AdServices token and the app facts.
// Works in classic Xamarin.iOS and in .NET for iOS. MIT licensed.
#if __IOS__
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AdServices;
using Foundation;
using UIKit;

namespace Fload.HttpAttribution
{
    public static class FloadIos
    {
        static FloadHttpAttribution instance;

        /// <summary>
        /// Call once in FinishedLaunching. Reports the install on the first launch and
        /// sends what is queued now and each time the app returns to the foreground.
        /// </summary>
        public static FloadHttpAttribution Start(string writeKey, Uri endpoint = null)
        {
            if (instance != null) return instance;
            instance = new FloadHttpAttribution(writeKey, StateDirectory(), endpoint);
            var facts = Facts();
            var started = instance.StartAsync(facts, ReadTokenAsync);
            UIApplication.Notifications.ObserveWillEnterForeground((sender, args) => { var flushed = instance.FlushAsync(); });
            return instance;
        }

        /// <summary>Links your app user id (e.g. RevenueCat's AppUserID). Safe before Start.</summary>
        public static Task SetUserIdAsync(string userId)
        {
            return instance == null ? Task.CompletedTask : instance.SetUserIdAsync(userId);
        }

        static FloadAppFacts Facts()
        {
            var info = NSBundle.MainBundle.InfoDictionary;
            return new FloadAppFacts
            {
                Platform = "ios",
                AppId = NSBundle.MainBundle.BundleIdentifier,
                AppVersion = info["CFBundleShortVersionString"]?.ToString(),
                AppBuild = info["CFBundleVersion"]?.ToString(),
                OsVersion = UIDevice.CurrentDevice.SystemVersion,
                DeviceModel = Environment.GetEnvironmentVariable("SIMULATOR_MODEL_IDENTIFIER") ?? Machine(),
                Locale = NSLocale.CurrentLocale.Identifier,
            };
        }

        // AAAttribution.attributionToken(): no IDFA, no tracking prompt. It blocks, so it runs off the main thread.
        static Task<FloadInstallSource> ReadTokenAsync()
        {
            return Task.Run(async () =>
            {
                if (!UIDevice.CurrentDevice.CheckSystemVersion(14, 3))
                    return new FloadInstallSource { Status = "unsupported_os" };
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    NSError error;
                    var token = AAAttribution.GetAttributionToken(out error);
                    if (error == null && !string.IsNullOrEmpty(token))
                        return new FloadInstallSource { Status = "token", AppleToken = token.Trim(), AppleTokenCreatedAt = DateTimeOffset.UtcNow };
                    if (error != null && error.Code == (long)AAAttributionErrorCode.PlatformNotSupported)
                        return new FloadInstallSource { Status = "unsupported_os" };
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);   // network or internal error
                }
                return new FloadInstallSource { Status = "token_error" };
            });
        }

        // Library/Application Support/<bundle id>, never backed up: a restored phone is a new install.
        static string StateDirectory()
        {
            var support = NSSearchPath.GetDirectories(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User)[0];
            var directory = Path.Combine(support, NSBundle.MainBundle.BundleIdentifier ?? "app");
            Directory.CreateDirectory(directory);
            NSUrl.FromFilename(directory).SetResource(NSUrl.IsExcludedFromBackupKey, NSNumber.FromBoolean(true));
            return directory;
        }

        [DllImport("libc")]
        static extern int sysctlbyname(string name, byte[] value, ref IntPtr length, IntPtr newValue, IntPtr newLength);

        // The hardware model, e.g. iPhone16,2.
        static string Machine()
        {
            var length = IntPtr.Zero;
            if (sysctlbyname("hw.machine", null, ref length, IntPtr.Zero, IntPtr.Zero) != 0 || length == IntPtr.Zero) return null;
            var buffer = new byte[length.ToInt64()];
            if (sysctlbyname("hw.machine", buffer, ref length, IntPtr.Zero, IntPtr.Zero) != 0) return null;
            return System.Text.Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }
    }
}
#endif
