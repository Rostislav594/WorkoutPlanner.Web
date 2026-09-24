# Design QA — header audio equalizer

**Source visual truth**

- Path: `C:\Users\User\AppData\Local\Temp\codex-clipboard-bb2e8027-60d0-4a24-b04e-6f8b0ca17373.png`
- Source pixels: 1920 × 1032.
- Relevant source region: right mobile wireframe at x=1043, y=134, 377 × 819 px.

**Rendered implementation**

- Screenshot: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-fine-bass.png`
- Comparison image: `C:\Users\User\AppData\Local\Temp\gymplanner-design-qa-comparison-fine.png`
- Isolated bass-zone evidence: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-zone-bass-isolated.png`
- Isolated high-zone evidence: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-zone-high-isolated.png`
- Filled monochrome bass evidence: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-white-filled.png`
- Focused filled monochrome crop: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-white-filled-crop.png`
- High-density device evidence: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-dense-final.png`
- Focused high-density crop: `C:\Users\User\AppData\Local\Temp\gymplanner-eq-dense-final-crop.png`
- Screenshot pixels: 1080 × 2340.
- WebView viewport: 392 × 851 CSS px at device pixel ratio 2.75, portrait.
- Density normalization: the implementation screenshot was downsampled to 377 × 819 px and placed beside the 377 × 819 px reference phone region.
- State: authenticated primary page, equalizer active during a short external 90 Hz bass impulse.

## Full-view comparison evidence

The reference is an annotated layout wireframe rather than a pixel-exact production screen. The implementation preserves its structural intent: brand at left, a dedicated equalizer region in the center, an unboxed toggle immediately to its right, and the existing profile action at the far right. The reference's red rectangle, arrows, and explanatory Russian sentence are annotations and were correctly not rendered as application UI. Existing application content and navigation remain unchanged.

## Focused region comparison evidence

The side-by-side comparison image shows the full reference phone and the normalized device capture in one image. The header detail is readable without an additional crop: the toggle has no border, background, or shadow; the particle waveform stays inside the central 165.7 × 44 CSS px slot; the 44 × 48 CSS px toggle target remains separate from both the waveform and profile control.

## Required fidelity surfaces

- Fonts and typography: the existing GymPlanner type hierarchy and Material Symbols icon family are preserved. The reference type is illustrative; no app typography was replaced.
- Spacing and layout rhythm: a three-column header grid keeps the center slot flexible and maintains 4–8 px spacing between header controls. No overlap or horizontal overflow was observed at 392 CSS px.
- Colors and visual tokens: the existing dark surface, white/gray foreground, and orange active token are retained. State is communicated by both `aria-pressed` and icon/dot color, not by a shadowed container.
- Image quality and asset fidelity: the reference contains no production image asset to reproduce. The existing wallpaper remains sharp and unchanged; the equalizer is intentionally generated from live particles.
- Copy and content: the wireframe's instructional copy is treated as annotation, not product copy. Existing localized accessible labels remain in use.

## Findings

No actionable P0, P1, or P2 differences remain within the requested header/equalizer scope.

## Comparison history

1. Initial device pass (`C:\Users\User\AppData\Local\Temp\gymplanner-header-tone.png`): P2 — the compact waveform reacted to audio but remained too flat to read clearly at a glance.
2. Fix: increased the responsive amplitude scale while retaining the 44 px clipping boundary and quiet-state baseline.
3. Final device pass (`C:\Users\User\AppData\Local\Temp\gymplanner-header-final.png`): the bass response forms a clearly visible multi-lobe particle wave, remains contained, and does not collide with adjacent controls.
4. Refinement request: make individual grains finer and make the resonance brighter and more immediate.
5. Fix: reduced particle radii from 0.35–1.07 to 0.20–0.68 CSS px, increased density to preserve continuity, narrowed the frequency lobes, changed spectrum and canvas smoothing to fast attack/short release, and added a brief additive beat accent.
6. Final device passes: `gymplanner-eq-fine-quiet.png` shows the finer quiet baseline; `gymplanner-eq-fine-bass.png` shows a brighter, sharply expanded 90 Hz response; `gymplanner-eq-fine-release.png` confirms the short decay after the impulse.
7. Zoning request: prevent the complete waveform from moving on every sound and form peaks of different heights only in the relevant regions.
8. Fix: split the symmetric waveform into four narrow bass, low-mid, mid, and high zones; select the strongest local response instead of summing overlapping bands; confine the beat impulse to the bass zone; reduce global horizontal drift; and vary peak height per particle.
9. Final device passes: `gymplanner-eq-zone-bass-isolated.png` shows one strong localized central bass peak while the remainder stays near baseline; `gymplanner-eq-zone-high-isolated.png` shows separate narrow outer high-frequency peaks while the center stays near baseline.
10. Fill and color request: remove the sparse interior of the central response and make every grain strictly white.
11. Fix: centered and widened the bass envelope, redistributed particles toward the waveform interior, increased particle density without increasing grain radius, and removed the orange color branch in favor of a single `255, 255, 255` particle color.
12. Final device pass: `gymplanner-eq-white-filled.png` shows a continuous filled central bass response. A sampled-pixel check of the focused crop found zero chromatic particles among 2,832 visible sampled pixels.
13. Density request: minimize the remaining empty space without making the grains larger.
14. Fix: raised the 165.7 CSS px header waveform from roughly 460 to 1,193 particles, precomputed static frequency weights, batched all particle circles into one Canvas fill operation, and removed decorative horizontal drift so the denser field remains stable.
15. Final device pass: `gymplanner-eq-dense-final.png` shows the denser white particle field under an isolated bass response. A 180-frame device measurement averaged 58.4 FPS with a 16.8 ms p95 frame interval.

## Interactions verified

- Toggle on: particles enter from the left and right and assemble in the header slot.
- Live response: the particle waveform expands immediately on a short external 90 Hz impulse and returns quickly after it ends.
- Frequency zoning: an isolated bass frame activates only the central zone; an isolated high-frequency frame activates only the outer zones. Inactive zones remain close to the baseline instead of moving as one continuous mass.
- Peak variation: zone multipliers and per-particle peak scaling produce localized peaks of different heights rather than a uniform full-width expansion.
- Interior fill: the centered bass envelope and interior-biased distribution remove the central notch while preserving a fine-grain particle texture.
- Monochrome rendering: all particles use white RGB values; no orange or other colored accent remains in the waveform.
- Dense rendering: the production-size header Canvas creates 1,193 fine particles on the verified phone while preserving responsive vertical frequency movement and near-60 FPS rendering.
- Navigation persistence: active state remained intact across `/`, `/workouts`, and `/workouts/199`.
- Toggle off: canvas returned to its inactive state and Android released the microphone recording track.
- Runtime state: no Blazor error overlay was visible during the tested flows.

## Implementation checklist

- [x] Unboxed equalizer toggle in the header.
- [x] Flexible centered particle visualizer.
- [x] Left/right entrance animation.
- [x] Responsive live audio reaction.
- [x] Accessible toggle name, pressed state, focus indicator, and touch target.
- [x] Reduced-motion behavior suppresses the entrance choreography.
- [x] Primary and secondary routes verified on the target phone.

## Follow-up polish

No blocking polish remains. The exact waveform height naturally varies with microphone gain and the external audio source.

final result: passed
