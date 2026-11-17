// Run with Node.js: node desktop-app/test-player.js
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
class Hls {
  static isSupported() { return true; }
  static Events = {ERROR: 'error', MANIFEST_PARSED: 'manifest'};
  static ErrorTypes = {NETWORK_ERROR: 'network', MEDIA_ERROR: 'media'};
  on(event, callback) { this[event] = callback; }
  loadSource(url) { this.source = url; }
  attachMedia(video) { this.video = video; }
  startLoad() {}
  recoverMediaError() {}
}
const gear = {style: {}};
const label = {textContent: ''};
const context = {
  URL, location: {href: 'http://127.0.0.1:1234/'}, Hls, window: {Hls},
  hlsEpoch: 0, videoErrorHooked: false, awaitingFirstFrame: true,
  loaderFallbackTimer: null, clearTimeout,
  loader: {querySelector: name => name === '.gear' ? gear : label, classList: {remove() {}}},
  video: {muted: true, events: {}, load() {}, addEventListener(event, callback) {this.events[event] = callback;}},
  stopAll() { context.hls = null; }, showLoader() {}, markActive() {}, playSafe() {}, emit() {}, log() {}
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(__dirname, 'player-video.js'), 'utf8'), context);
const proxied = source => 'http://127.0.0.1:1234/hls?u=' + encodeURIComponent(source);
assert.equal(context.directMediaUrl(proxied('http://cdn.bilivideo.com/video.mp4?sig=x%2Fy&n=1')), 'https://cdn.bilivideo.com/video.mp4?sig=x%2Fy&n=1');
assert.equal(context.directMediaUrl(proxied('https://cdn.example/video.webm')), 'https://cdn.example/video.webm');
for (const source of ['http://cdn.example/video.mp4', 'http://bilivideo.com.evil/video.mp4', 'https://user:pass@example.com/video.mp4', 'https://example.com/stream.m3u8']) {
  assert.equal(context.directMediaUrl(proxied(source)), null);
}
context.startHls(proxied('http://cdn.bilivideo.com/video.mp4'), 1);
assert.equal(context.video.src, 'https://cdn.bilivideo.com/video.mp4');
assert.equal(context.video.muted, false);
assert.equal(context.hls, null);
context.video.error = {code: 4};context.video.events.error();
assert.equal(gear.style.display, 'none');
assert.match(label.textContent, /could not be played/);
context.startHls(proxied('https://cdn.example/stream.m3u8'), 2);
assert.ok(context.hls instanceof Hls);
assert.equal(context.hls.source, proxied('https://cdn.example/stream.m3u8'));
assert.equal(context.hlsEpoch, 2);
console.log('PASS: direct MP4 routing, signed-query preservation, HTTPS upgrade scope, existing HLS routing, and playback error feedback.');
