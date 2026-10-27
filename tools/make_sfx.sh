#!/bin/bash
# Cuts the game's sound clips (Assets/Audio/Resources/Sfx) out of the raw downloads listed in Assets/Audio/CREDITS.md.
# tools/make_sfx.sh "$HOME/Desktop/sound sources" Assets/Audio/Resources/Sfx
set -e
SRC="$1"; OUT="$(mkdir -p "$2" && cd "$2" && pwd)"; cd "$SRC"
TMP="$OUT/.tmp.wav"
FMT="-ar 44100 -ac 1 -c:a pcm_s16le"
ON="silenceremove=start_periods=1:start_threshold=-40dB"

# norm NAME PEAK_DB : rescale the clip so its peak sits at PEAK_DB
norm() {
  local f="$OUT/$1.wav" peak
  peak=$(ffmpeg -hide_banner -i "$f" -af volumedetect -f null - 2>&1 | sed -n 's/.*max_volume: \(-\?[0-9.]*\) dB/\1/p')
  ffmpeg -v error -y -i "$f" -af "volume=$(awk "BEGIN{print $2 - ($peak)}")dB" $FMT "$TMP" && mv "$TMP" "$f"
  echo "$1: $(ffprobe -v error -show_entries format=duration -of csv=p=0 "$f") s"
}
cut() { ffmpeg -v error -y "$@"; }

# Sun: mark appears, two variants generated with ElevenLabs Sound Effects (A2 stone sigil, B2 golden glow)
cut -i elevenlabs_sun_mark_A2_stone_sigil.wav -af "silenceremove=start_periods=1:start_threshold=-50dB,afade=t=out:st=1.4:d=0.5" $FMT "$OUT/sun_mark_1.wav"; norm sun_mark_1 -6
cut -i elevenlabs_sun_mark_B2_golden_glow.wav -af "afade=t=in:d=0.03,afade=t=out:st=1.3:d=0.7" $FMT "$OUT/sun_mark_2.wav"; norm sun_mark_2 -6
# Sun: charge, 13 candidates while one is chosen in the playground. Each is 0.5 s and ends on its loudest point.
# 1 is the reverse cymbal (lower); 2-13 are ElevenLabs generations E1-E4, F1-F4, G1-G4 (prompts in CREDITS.md).
cut -ss 1.82 -t 0.40 -i 130387*.aiff -af "aresample=44100,asetrate=35280,aresample=44100,afade=t=in:d=0.1" $FMT "$OUT/sun_charge_1.wav"; norm sun_charge_1 -4
n=2
for c in E1:0 E2:0 E3:0 E4:0 F1:0 F2:0 F3:0 F4:0 G1:0.15 G2:0 G3:0.05 G4:0.1; do
  cut -ss "${c#*:}" -t 0.5 -i "elevenlabs_sun_charge_${c%%:*}.wav" -af "afade=t=in:d=0.03,afade=t=out:st=0.49:d=0.01" $FMT "$OUT/sun_charge_$n.wav"; norm "sun_charge_$n" -4
  n=$((n+1))
done
# Sun: impact (powerful hit, a little lower, over the sub-bass drop and a roaring burst of flame)
cut -i 855910*.mp3 -i 856174*.mp3 -i 490166*.flac -filter_complex "[0]aresample=44100,asetrate=37485,aresample=44100,atrim=0:2.5[h];[1]aresample=44100,atrim=0:2.5,volume=-6dB,afade=t=out:st=0.4:d=2.1[s];[2]aresample=44100,atrim=0.95:3.45,asetpts=PTS-STARTPTS,volume=-9dB,afade=t=out:st=0.5:d=2[f];[h][s][f]amix=inputs=3:duration=longest:normalize=0,afade=t=out:st=1.5:d=1" $FMT "$OUT/sun_impact.wav"; norm sun_impact -1
# Sun: the scorch steaming as it cools (the water-on-a-hot-stove variant was dropped)
cut -ss 12.4 -t 3 -i 872946*.wav -af "volume=32dB,alimiter=limit=0.5:attack=2:release=60,afade=t=in:d=0.05,afade=t=out:st=1:d=2" $FMT "$OUT/sun_sizzle.wav"; norm sun_sizzle -8

# Moon: cast (cymbal swell, peaks about 0.8 s in)
cut -ss 23.75 -t 1.7 -i 826131*.wav -af "afade=t=in:d=0.05,afade=t=out:st=0.9:d=0.8" $FMT "$OUT/moon_cast.wav"; norm moon_cast -6
# Moon: absorb, two variants (wine glass, crystal ping)
cut -i 198401*.wav -af "$ON,atrim=0:1.2,afade=t=out:st=0.4:d=0.8" $FMT "$OUT/moon_absorb_1.wav"; norm moon_absorb_1 -4
cut -i 542561*.wav -af "$ON,atrim=0:1.5,afade=t=out:st=0.5:d=1" $FMT "$OUT/moon_absorb_2.wav"; norm moon_absorb_2 -4
# Moon: shatter (glass break with a faint ring under it)
cut -i 221528*.flac -i 198401*.wav -filter_complex "[0]aresample=44100,$ON,atrim=0:1.2[g];[1]aresample=44100,$ON,volume=-9dB[r];[g][r]amix=inputs=2:duration=first:normalize=0,afade=t=out:st=0.6:d=0.6" $FMT "$OUT/moon_shatter.wav"; norm moon_shatter -2
# Moon: fade (soft whoosh)
cut -i 715784*.wav -af "lowpass=f=5000,afade=t=in:d=0.1,afade=t=out:st=0.4:d=0.47" $FMT "$OUT/moon_fade.wav"; norm moon_fade -8

# Lightning: a far-off rumble under the sky flickers (a roll from the TRP thunder, muffled)
cut -ss 12.0 -t 1.1 -i 616992*.wav -af "lowpass=f=300,lowpass=f=300,afade=t=in:d=0.15,afade=t=out:st=0.4:d=0.7" $FMT "$OUT/lightning_rumble.wav"; norm lightning_rumble -10
# Lightning: strike (a close crack and a short roll; highs cut and kept a little quieter than the sun impact)
cut -ss 7.44 -t 2.8 -i 243614*.wav -af "lowpass=f=6000,afade=t=in:d=0.02,afade=t=out:st=1.6:d=1.2" $FMT "$OUT/lightning_strike.wav"; norm lightning_strike -9
