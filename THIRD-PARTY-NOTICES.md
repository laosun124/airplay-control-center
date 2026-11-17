# Third-party notices

## Java AirPlay

Project: https://github.com/serezhka/java-airplay

Author: Sergei Fedorov (serezhka)

Upstream version: v1.1.1

License: MIT. The complete upstream notice is retained in `desktop-app/LICENSE.upstream` and included in release packages.

The local build modifies `ChromiumHost.class` and `mse/player.html`. The browser starts in a normal 1120 x 760 window, uses receiver-specific profile preferences to suppress translation and video-overlay prompts, and marks the player page as non-translatable with picture-in-picture disabled for its video element. The source of these changes and the build script are included in this repository.

The upstream executable JAR also includes third-party libraries, such as Spring Boot, Netty, JmDNS, and SLF4J, and bundled browser playback resources. Their original archives, license files, and notices are retained within the upstream JAR. These components remain subject to their respective licenses.

## Java runtime

No Java runtime is included in this project's Windows ZIP. Users install Java 21 or later separately. The included `Download Java.html` links to the official Oracle Java download page. Oracle JDK is governed by the license terms shown on the Oracle download page.

## Application artwork

The application icon was generated for this project using OpenAI image generation. It is not an Apple or Microsoft brand asset.
