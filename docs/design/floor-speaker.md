# Wallpaper floor speaker

`GymPlanner.Mobile/wwwroot/images/floor-speaker.png` was generated with the
built-in ImageGen tool. It is a 1024 × 1536 RGBA asset with a transparent
background. The source wallpaper and home logo remain unchanged.

`FloorSpeaker.razor` reuses this photograph for the stationary cabinet and two
clipped membrane layers. The membranes and emitted sound arcs animate. The layout moves the
speaker stage using the same shift, duration and easing as the wallpaper.
Reduced-motion preferences disable both the vibration and scene transition.
The speaker is decorative, hidden from accessibility navigation, and has no
audio playback, microphone capture or interactive controls.

The speaker is anchored near the right edge, reserving the maximum positive
wallpaper shift of 8vw and a safe screen gutter. Transparent image padding is
excluded from the visible-cabinet margin. Subtle silver arcs expand outward
on both sides of both woofers to make the animation readable against the dark floor;
reduced-motion preferences also hide these arcs.
Their peak opacity is limited to 22% and stroke width to 0.8 CSS pixels so the
requested sound effect stays subdued.
Membrane pulses expand up to 5.5% and contract by 1.5%, with one pulse every
0.6 seconds. The cabinet and outer driver rims remain stationary.

The right-edge update was checked on Redmi Note 8 across all five primary
tabs, including intermediate transition frames. The visible cabinet retained
about 17 CSS pixels of right clearance at the maximum positive shift.
Viewport checks at 320 × 740, 375 × 812 and 851 × 392 retained at least
13 CSS pixels without horizontal overflow. Reduced-motion emulation hid the
sound arcs. These checks use the visible cabinet's alpha bounds rather than
the photograph's transparent padding.

Final tuning was rechecked after installing and restarting the APK: four sound
emitters (both sides of each membrane), 0.8px strokes, approximately 22% peak
opacity, membrane scale from about 0.986 to 1.054, and measured main pulse
intervals of 599–601ms. Cabinet and visible sound arcs remained inside the
screen throughout primary-tab transitions. No temporary preview styles were
used for this final behavior check.

## Files and verification

- Added `Components/Layout/FloorSpeaker.razor`, its scoped CSS and the image
  in `GymPlanner.Mobile`.
- Updated `MainLayout.razor` and its CSS to remove the header audio UI and move
  the speaker with the wallpaper. Preserved application navigation and page error handling.
- Added directional entrance slides for primary page content, using the same
  0.5-second easing as the wallpaper and determining direction before rendering
  the new route. Header and bottom navigation remain fixed.
- Adjusted `Home.razor.css` in landscape to reserve space below the identity.
- Removed the six files under `GymPlanner.Mobile/Audio`, Android's
  `AndroidAudioSpectrumService.cs`, and the `audioEqualizer.js` and
  `decorativeEqualizer.js` modules.
- Removed audio service registration, the Razor namespace import, Android's
  `RECORD_AUDIO` permission and the unused `AudioEqualizer_*` / `AudioMode_*`
  entries in all three localization resource files. Updated obsolete CSS
  comments and marked the earlier header equalizer design review as historical.

Final Android Debug build succeeded. Installed and exercised on the connected
Redmi Note 8 through its WebView debugging interface, with native screen captures
and a screen recording. Confirmed the old UI is absent, the image loads, the
cabinet stays still while both membranes animate, wallpaper and speaker shifts
match throughout navigation, secondary routes remove the decorative scene,
and reduced-motion emulation stops vibration. Checked 375 × 812 and
851 × 392 WebView viewport emulation without horizontal overflow.

Physical landscape rotation could not be exercised: this phone denies ADB
`WRITE_SETTINGS`. Its original rotation settings remained unchanged.
System ADB touch injection is also restricted; interactions used the permitted
WebView touch interface. iOS was not built or exercised in this Windows task.

## Generation prompt

Use case: product-mockup. Asset type: transparent photorealistic foreground asset for an existing mobile gym app wallpaper. Generate ONE compact floor-standing music loudspeaker, isolated on a genuinely transparent background, no environment. It will stand on the dark rubber tiled floor beneath an existing white 'GYM and nothing else' logo. Match a nearly monochrome black industrial gym interior with dim cool window light from upper right and very faint soft front fill. Rugged charcoal-black rectangular cabinet, subtle worn textured finish and black metal screws; TWO exposed circular dark woofer cones vertically aligned and a small dark tweeter above, rubber surrounds and dust caps clearly visible, no grille covering the cones. Front face mostly straight-on and centered, very slight view of its right side and top consistent with a camera above floor level, no fisheye, all front cone circles remain almost circular for separate small animation. Real photographic materials, restrained satin gray highlights, a little dust, believable weight, feet on floor, tight faint transparent contact shadow beneath. The whole speaker fits fully in the canvas with modest transparent margins, large object filling 85% canvas height. No logos, no lettering, no neon, no colored LEDs, no cartoon, no white background, no room, no floor plane or opaque background. Goal: look naturally part of a dark gym photo, not a UI icon.
