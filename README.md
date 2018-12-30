# Xmmk: cross-platform virtual MIDI keyboard

Xmmk is a tiny virtual MIDI keyboard application based on managed-midi API. It is almost all about managed-midi API dogfooding.

It supports two keyboard layout modes

- Piano - that you know. When there is no half note e.g. `e-sharp` or `b-sharp` then there is no corresponding key.
- ChromaTone - every key has an assigned note. That means, the key right next to `e` is `f+` because `f` is placed on right-upper next to `e`.

 (And, the layout basis is weird - I only have JP106 keyboard so only alphabets are likely to work as expected.)

When you type "notes" then they will be recorded at the text entry box, as simple MML. When it was typed while there are other notes, then there will be `&` meaning that they consist of chord notes.

SHIFT+UP increases octave, SHIFT+DOWN decreases it. SHIFT+LEFT decreases transpose, SHIFT+RIGHT increases it.

![screenshot](screenshot.png)
