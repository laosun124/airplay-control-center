# Third-party notices

## Java AirPlay

Project: https://github.com/serezhka/java-airplay

Author: Sergei Fedorov (serezhka)

Upstream version: v1.1.1

License: MIT. The complete upstream notice is retained in `desktop-app/LICENSE.upstream` and included in release packages.

The local windowed build modifies only `ChromiumHost.class`. The browser startup argument `--start-fullscreen` is replaced by `--window-size=1120,760`. The source of this change and its build script are included in this repository.

The upstream executable JAR also includes third-party libraries, such as Spring Boot, Netty, JmDNS, and SLF4J, and bundled browser playback resources. Their original archives, license files, and notices are retained within the upstream JAR. These components remain subject to their respective licenses.

## Java runtime

No Java runtime is included in this project's Windows ZIP. Users install Java 21 or later separately. The included `Download Java.html` links to the official Eclipse Adoptium download page. Eclipse Temurin and other Java distributions are governed by their respective licenses.

## Application artwork

The application icon was generated for this project using OpenAI image generation. It is not an Apple or Microsoft brand asset.
