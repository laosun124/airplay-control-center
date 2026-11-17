package com.github.serezhka.airplay.app.mse;



import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Launches system Chromium/Chrome against the local MSE page (normal resizable {@code --app=} window).
 * Starts lazily on the first mirror/HLS session — same idea on every OS as GST/FFmpeg opening
 * a normal resizable sink. The page shows a spinner until the first decoded frame.
 */
final class ChromiumHost implements AutoCloseable {
    private static final org.slf4j.Logger log = org.slf4j.LoggerFactory.getLogger(ChromiumHost.class);

    private final String pageUrl;
    private final Path userData;
    private volatile Process process;
    private volatile String browserBin;

    ChromiumHost(String pageUrl) throws IOException {
        this.pageUrl = pageUrl;
        this.userData = Files.createTempDirectory("airplay-mse-chromium-");
        this.userData.toFile().deleteOnExit();
        configureProfile(this.userData);
        this.browserBin = resolveBrowser();
    }

    /** {@code true} if a Chromium/Chrome binary was found at construction time. */
    boolean browserAvailable() {
        return browserBin != null && !browserBin.isBlank();
    }

    synchronized boolean isAlive() {
        Process p = process;
        return p != null && p.isAlive();
    }

    /**
     * Ensure a browser is running. Returns true if a new process was started.
     */
    synchronized boolean ensureAlive() throws IOException {
        if (isAlive()) {
            return false;
        }
        if (!browserAvailable()) {
            throw new IllegalStateException(
                    "No Chromium/Chrome found (needed for MSE). Install a browser or set -Dairplay.mse.browser=");
        }
        log.info("MSE Chromium launching url={}", pageUrl);
        destroyQuietly();
        startProcess();
        return true;
    }

    /** Kill and start fresh (e.g. process alive but control WebSocket gone). */
    synchronized void restart() throws IOException {
        log.warn("MSE Chromium restart url={}", pageUrl);
        destroyQuietly();
        startProcess();
    }

    private static boolean headless() {
        return Boolean.parseBoolean(System.getProperty("airplay.mse.headless", "false"));
    }

    static void configureProfile(Path userData) throws IOException {
        // Preferences belong to this receiver's isolated browser profile.
        Path profile = Files.createDirectories(userData.resolve("Default"));
        Files.writeString(profile.resolve("Preferences"), "{\"translate\":{\"enabled\":false},\"edge\":{\"show_pip_overlay_control\":false,\"video_translate\":{\"settings_toggle_pref\":false,\"live_video_translation_enabled\":false}},\"video_hover_overlay\":{\"enabled\":false}}");
    }

    private void startProcess() throws IOException {
        List<String> cmd = buildLaunchCommand(browserBin, userData, pageUrl, headless());
        ProcessBuilder pb = new ProcessBuilder(cmd);
        pb.redirectErrorStream(true);
        pb.redirectOutput(ProcessBuilder.Redirect.DISCARD);
        process = pb.start();
        log.info("MSE Chromium started pid={} url={}", process.pid(), pageUrl);
    }

    /**
     * Chromium argv for the MSE player window. Headed builds always use
     * {@code --window-size=1120,760}; native maximize keeps the Windows taskbar.
     */
    static List<String> buildLaunchCommand(String browserBin, Path userData, String pageUrl, boolean headless) {
        List<String> cmd = new ArrayList<>();
        cmd.add(browserBin);
        cmd.add("--user-data-dir=" + userData.toAbsolutePath());
        cmd.add("--no-first-run");
        cmd.add("--no-default-browser-check");
        cmd.add("--disable-sync");
        cmd.add("--disable-features=Translate,TranslateUI,msEdgeTranslate,msTranslateVideo");
        cmd.add("--autoplay-policy=no-user-gesture-required");
        cmd.add("--disable-session-crashed-bubble");
        cmd.add("--check-for-update-interval=31536000");
        if (headless) {
            cmd.add("--headless=new");
            cmd.add("--disable-gpu");
        } else {
            cmd.add("--window-size=1120,760");
        }
        if (Boolean.parseBoolean(System.getProperty("airplay.mse.chrome.noSandbox", "false"))) {
            cmd.add("--no-sandbox");
            cmd.add("--disable-dev-shm-usage");
        }
        cmd.add("--app=" + pageUrl);
        return cmd;
    }

    private static String resolveBrowser() {
        return MseBrowsers.resolveForPlayback();
    }

    /**
     * Tear down the browser process (e.g. AirPlay client disconnected). Next session
     * starts a fresh normal resizable window via {@link #ensureAlive()}.
     */
    synchronized void stop() {
        destroyQuietly();
    }

    private void destroyQuietly() {
        Process p = process;
        process = null;
        if (p == null) {
            return;
        }
        log.info("MSE Chromium stopping pid={}", p.pid());
        p.destroy();
        try {
            if (!p.waitFor(2, TimeUnit.SECONDS)) {
                p.destroyForcibly();
                p.waitFor(3, TimeUnit.SECONDS);
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            p.destroyForcibly();
        }
    }

    @Override
    public synchronized void close() {
        destroyQuietly();
    }
}
