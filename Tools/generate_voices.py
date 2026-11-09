#!/usr/bin/env python3
"""
Generates placeholder voice clips for every line of dialogue, using macOS text-to-speech.

  python3 Tools/generate_voices.py            # make clips for new or changed lines
  python3 Tools/generate_voices.py --force    # remake every clip
  python3 Tools/generate_voices.py --list     # just list the lines, make nothing

It scans the game's C# code for dialogue (any Speakers.X followed by a text in quotes), records each line
with the voice set for that speaker below, and saves it as Assets/_Project/Resources/Voice/<code>.wav.
The game finds clips by that code (VoiceBank.Key), so names must not be changed by hand.
Tools/voice_lines.csv lists every code with its speaker and text: use it as the script when recording
real voice actors, then save each recording over the matching placeholder file.
Placeholder files that no line uses anymore are deleted.
"""
import csv, os, re, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CODE = os.path.join(ROOT, "Assets", "_Project")
OUT = os.path.join(CODE, "Resources", "Voice")
CSV = os.path.join(ROOT, "Tools", "voice_lines.csv")

# Speaker constant -> (label shown in game, macOS voice, words per minute)
VOICES = {
    "Commander":      ("COMMANDER",       "Samantha", 175),
    "Pilot":          ("COLONY PILOT",    "Daniel",   178),
    "ShipAI":         ("SHIP AI",         "Fred",     165),
    "FlightDirector": ("FLIGHT DIRECTOR", "Reed (English (US))", 172),
    "Controller":     ("CONTROLLER",      "Eddy (English (US))", 185),
    "Network":        ("NETWORK",         "Fred",     140),
}

STRING = re.compile(r'(?<![$@\w])"((?:[^"\\\n]|\\.)*)"')


def unescape(text):
    return re.sub(r'\\(u[0-9a-fA-F]{4}|.)', lambda m: chr(int(m.group(1)[1:], 16)) if m.group(1)[0] == "u" and len(m.group(1)) == 5
                  else {"n": "\n", "t": "\t"}.get(m.group(1), m.group(1)), text)


def key(label, text):
    """FNV-1a 32-bit of 'label|text', matching VoiceBank.Key in C#."""
    h = 2166136261
    for b in (label + "|" + text).encode("utf-8"):
        h = ((h ^ b) * 16777619) & 0xFFFFFFFF
    return f"{h:08x}"


def find_lines():
    """Every (speaker, text) in the code: the quoted texts after a Speakers.X, up to the end of that call or tuple."""
    lines = []
    for folder, _, files in os.walk(CODE):
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(folder, name)
            source = open(path, encoding="utf-8").read()
            for match in re.finditer(r"Speakers\.(\w+)", source):
                speaker = match.group(1)
                if speaker not in VOICES:
                    continue
                depth, i = 0, match.end()
                while i < len(source):
                    ch = source[i]
                    if ch == '"':
                        m = STRING.match(source, i)
                        if m and not source[i - 1] in "$@":
                            lines.append((speaker, unescape(m.group(1)), os.path.relpath(path, ROOT)))
                            i = m.end()
                            continue
                    if ch in "([{":
                        depth += 1
                    elif ch in ")]}":
                        if depth == 0:
                            break
                        depth -= 1
                    elif ch == ";" or (ch == "," and depth == 0 and source[i + 1:i + 30].lstrip().startswith("Speakers.")):
                        break
                    i += 1
    seen, unique = set(), []
    for line in lines:
        if line[:2] not in seen:
            seen.add(line[:2])
            unique.append(line)
    return unique


def speakable(text):
    """Cleans up text for the speech engine: dashes and ellipses become pauses."""
    return text.replace("—", ", ").replace("...", ", ").replace("'", "’")


def record(voice, rate, text, wav):
    with tempfile.TemporaryDirectory() as tmp:
        aiff = os.path.join(tmp, "line.aiff")
        subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", aiff, speakable(text)], check=True)
        subprocess.run(["afconvert", "-f", "WAVE", "-d", "LEI16@22050", "-c", "1", aiff, wav], check=True)


def main():
    force = "--force" in sys.argv
    lines = find_lines()
    if "--list" in sys.argv:
        for speaker, text, where in lines:
            print(f"{VOICES[speaker][0]:16} {text}   ({where})")
        print(f"{len(lines)} lines")
        return

    os.makedirs(OUT, exist_ok=True)
    wanted, made = set(), 0
    with open(CSV, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["file", "speaker", "text", "source"])
        for speaker, text, where in lines:
            label, voice, rate = VOICES[speaker]
            code = key(label, text)
            wanted.add(code)
            writer.writerow([code + ".wav", label, text, where])
            wav = os.path.join(OUT, code + ".wav")
            if force or not os.path.exists(wav):
                record(voice, rate, text, wav)
                made += 1
                print(f"  {label:16} {text[:70]}")

    removed = 0
    for name in os.listdir(OUT):
        stem, ext = os.path.splitext(name)
        if ext in (".wav", ".meta") and stem.split(".")[0] not in wanted:
            os.remove(os.path.join(OUT, name))
            removed += ext == ".wav"
    print(f"{len(lines)} lines, {made} recorded, {removed} unused clips removed. Script: Tools/voice_lines.csv")


if __name__ == "__main__":
    main()
