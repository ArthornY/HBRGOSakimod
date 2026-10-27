// Transcription of the incoming-damage box in Cantata Kamen's own summary 2,
// dated 2025-08-03. This is a community research formula, not official source.
// Article: https://note.com/gazou774/n/n82b99b359f2b
// Image: https://assets.st-note.com/img/1756949797-JLWeI3ChdsDx1EAyXfgiN09Z.png
// Reproduce the displayed expressions directly. Attack multiplier is 1 here;
// the source does not establish exact final rounding or minimum/multiplier order.
function incomingPower(enemy, defense, minimum, maximum, threshold) {
  if (defense >= enemy + threshold) return 1;
  if (defense <= enemy - threshold) return maximum;
  if (defense >= enemy)
    return Math.max(1, (minimum / threshold) * (enemy + threshold - defense));
  return ((maximum - minimum) / threshold) * (enemy - defense) + minimum;
}
module.exports = incomingPower;
