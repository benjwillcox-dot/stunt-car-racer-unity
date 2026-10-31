// Gamepad rumble for the WebGL build. The Input System can't drive pad motors in a browser,
// so GameInput.SetRumble calls this, which uses the browser's Gamepad API instead. Chrome and
// Edge support it; in browsers whose pads have no vibrationActuator it does nothing.
mergeInto(LibraryManager.library, {
  // strong/weak are 0..1 for the heavy and light motors; 0 ms stops the rumble.
  StuntCarRumble: function (strong, weak, milliseconds) {
    var pads = [];
    try { pads = navigator.getGamepads ? navigator.getGamepads() : []; } catch (e) { return; }
    for (var i = 0; i < pads.length; i++) {
      var actuator = pads[i] && pads[i].vibrationActuator;
      if (!actuator) continue;
      var done = milliseconds > 0
        ? actuator.playEffect("dual-rumble", { startDelay: 0, duration: milliseconds, strongMagnitude: strong, weakMagnitude: weak })
        : actuator.reset && actuator.reset();
      if (done && done.catch) done.catch(function () {});
    }
  }
});
