  function directMediaUrl(url) {
    try {
      var proxy = new URL(url, location.href);
      var source = new URL(proxy.pathname === '/hls' ? (proxy.searchParams.get('u') || url) : url, location.href);
      if (!/\.(mp4|m4v|mov|webm)$/i.test(source.pathname) || source.username || source.password) return null;
      // Bilibili advertises HTTP MP4 links; its CDN serves the same signed URL over HTTPS.
      if (source.protocol === 'http:' && /(^|\.)bilivideo\.com$/i.test(source.hostname) && !source.port) source.protocol = 'https:';
      if (source.protocol !== 'https:' && !(source.protocol === 'http:' && /^(127\.0\.0\.1|localhost)$/.test(source.hostname))) return null;
      return source.href;
    } catch (e) { return null; }
  }

  function showMediaError(message) {
    awaitingFirstFrame = false;
    if (loaderFallbackTimer) { clearTimeout(loaderFallbackTimer); loaderFallbackTimer = null; }
    loader.querySelector('.gear').style.display = 'none';
    loader.querySelector('.loader-label').textContent = message;
    loader.classList.remove('hidden');
  }

  function startHls(url, epoch) {
    stopAll();
    hlsEpoch = epoch != null ? Number(epoch) : (hlsEpoch + 1);
    mode = 'hls';
    mirrorWsUrl = null;
    showLoader();
    markActive();
    video.muted = false;
    if (!videoErrorHooked) {
      videoErrorHooked = true;
      video.addEventListener('error', function () {
        if (mode !== 'hls') return;
        emit({ event: 'error', message: 'video element error code=' + (video.error ? video.error.code : 'unknown') });
        showMediaError('Video could not be played. Please reconnect or try another video.');
      });
    }
    var direct = directMediaUrl(url);
    if (direct) {
      video.src = direct;
      video.load();
      playSafe();
      log('direct video load epoch=' + hlsEpoch);
      return;
    }
    if (!window.Hls || !Hls.isSupported()) {
      emit({ event: 'error', message: 'Hls.js / MSE not supported' });
      showMediaError('Video streaming is not supported by this browser.');
      return;
    }
    hls = new Hls({
      enableWorker: true,
      lowLatencyMode: false,
      backBufferLength: 30,
      maxBufferHole: 1.5,
      nudgeMaxRetry: 5
    });
    hls.on(Hls.Events.ERROR, function (_e, data) {
      if (data && data.fatal) {
        emit({ event: 'error', message: 'hls fatal ' + data.type + '/' + data.details });
        showMediaError('Video could not be loaded. Please reconnect or try another video.');
        try {
          if (data.type === Hls.ErrorTypes.NETWORK_ERROR) hls.startLoad();
          else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) hls.recoverMediaError();
        } catch (e) {}
      }
    });
    hls.on(Hls.Events.MANIFEST_PARSED, function () {
      log('hls manifest parsed epoch=' + hlsEpoch);
      playSafe();
    });
    hls.loadSource(url);
    hls.attachMedia(video);
    log('hls loadSource epoch=' + hlsEpoch);
  }

