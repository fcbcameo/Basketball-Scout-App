using Foundation;

namespace BasketballScout.App;

/// <summary>
/// Adopts the UIScene lifecycle. Apps built with the iOS 27 SDK must use scenes — without one,
/// iOS terminates the app at launch (UIKit's "no scene lifecycle adoption" check). MAUI's
/// <see cref="MauiUISceneDelegate"/> does the window setup; it's wired up by the
/// UIApplicationSceneManifest entry in Info.plist, whose UISceneDelegateClassName must match
/// the registered name below.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
