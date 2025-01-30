# Song and Chart Timing

## Song Timing vs. Chart Timing

In StepMania and in `GrooveAuthor`, there are songs and charts. Songs have one or more charts. Some data is defined on the song, like the title, artist, and banner. Other data is defined per chart, like the difficulty and steps. Historically, events which affect timing were defined on the song and not the chart. The older `sm` format requires this. This means different charts for the same song cannot independently specify distinct rate gimmicks (e.g. "ACE FOR ACES" in DDR and many songs in PIU) and it means songs where each chart should use different music are unsupported (e.g. "Scripted Connection" or "Crew" from beatmania IIDX). Both of these restrictions are not good. It would be better if timing events were defined per chart rather than per song.

In StepMania 5 the `ssc` format was introduced which addresses this problem and allows for specifying timing data on the charts instead of the songs. However, StepMania's editor still prefers song timing. Timing events can be per-song or per-chart and it is often unclear when you are editing these events which charts are affected.

## GrooveAuthor Uses Chart Timing

`GrooveAuthor` always uses chart timing and never uses song timing. All events, including timing events, are per-chart. To be explicit, these events are always only on the chart and never on the song:
- Time Signature (`#TIMESIGNATURES`)
- Tempo (`#BPMS`)
- Stop (`#STOPS`)
- Delay (`#DELAYS`)
- Warp (`#WARPS`)
- Scroll Rate (`#SCROLLS`)
- Interpolated Scroll Rate (`#SPEEDS`)
- Fake Region (`#FAKES`)
- Combo Multipliers (`#COMBOS`)
- Ticks Count (`#TICKCOUNTS`)
- Label (`#LABELS`)

By keeping events always on the charts rather than the song, there is no confusion about what is affected by edits.

## Keeping Chart Timing In Sync

Often, the timing events should be the same between all charts in a song. To make this easy `GrooveAuthor` offers the following tools.

Right clicking any chart in the `Chart List` window brings up options for synchronizing timing events between two charts, or between all charts.

![Song Sync Before Compensation](timing-copy-events.png "Right click any chart to copy timing events between charts.")

The groups in this context menu encompass the following events.

| Event                    | Non-Step Event | Scroll Event | Timing Event |
|--------------------------|----------------|--------------|--------------|
| Time Signature           | X              |              | X            |
| Tempo                    | X              |              | X            |
| Stop                     | X              |              | X            |
| Delay                    | X              |              | X            |
| Warp                     | X              |              | X            |
| Scroll Rate              | X              | X            |              |
| Interpolated Scroll Rate | X              | X            |              |
| Fake Region              | X              |              |              |
| Multipliers              | X              |              |              |
| Tick Counts              | X              |              |              |
| Label                    | X              |              |              |
| Pattern                  |                |              |              |

Additionally, the `Copy Events` window accessible from the menu bar via `Chart` > `Advanced Event Copy...` will let you specify exactly which events you would like to copy.

![Advanced Event Copy](timing-advance-event-copy.png "The Copy Events window provides options for copying specific events.")

## Timing Chart

Unfortunately there is a bug in StepMania where if certain timing data is not specified on the song then some animations will play at the wrong speed. Even on a valid song with all charts specifying full, valid timing data, StepMania only examines song timing data for animations on the song wheel like cursor pulsing, and some animations during play like receptor pulsing. In the absence of valid song data StepMania falls back to using a tempo of 60 BPM for these animations.

To work around this issue `GrooveAuthor` always assigns one chart as the song's `Timing Chart`. The timing data from the `Timing Chart` is applied to the song when saving so that StepMania will not play animations incorrectly. When `GrooveAuthor` chooses a `Timing Chart` it prefers your `Default Type` and `Default Difficulty` specified in the `Options` window. However, the `Timing Chart` can always be assigned explicitly in the `Song Properties` window. 

![Timing Chart](timing-timing-chart.png "The Timing Chart's timing data is applied to the song when saving.")

Buttons to `Apply Timing` and `Apply Timing + Scroll` are present by the `Timing Chart` to quickly apply its timing events to all other charts.

## Saving `sm` Files

If your setup is locked to an older version of StepMania that does not have support for `ssc` files then you may want to save in the legacy `sm` format. If your setup does not strictly require use of the `sm` format, then you should use the `ssc` format.

When saving to the legacy `sm` format which requires only one set of timing events that live on the song, the `Timing Chart`'s events will be used for the song.

### `sm` Save Errors

If any chart contains events which aren't compatible with the `sm` format, *and those events would produce a chart where note timing would be affected by their absence*, then an error is displayed and the song will not be saved unless those events are removed. `GrooveAuthor` treats the following events as incompatible and will log errors when they are present:
- Warps (`#WARPS`)

Warps can be converted to negative stops for compatibility with the `sm` format by selecting `Edit` > `Convert All` > `Warps to Negative Stops`.

Additionally, if any chart has timing data which differs from the `Timing Chart` then an error is displayed and the song will not be saved. This behavior can be overridden by unchecking `Require Identical Timing in SM Files` under `Advanced Save Options`.

### `sm` Save Warnings

If `Require Identical Timing in SM Files` is disabled, then instead of an error being logged a warning will be logged when any chart has timing data which differs from the `Timing Chart` and the song will still save using the `Timing Chart`'s data.

Any events which are not supported in the `sm` format but do not affect timing will also only be treated as warnings and they will be omitted when saving. The events supported in `sm` files depends on the version of StepMania. `GrooveAuthor` treats the following events as incompatible and will log warnings when they are present:
- Scroll Rates (`#SCROLLS`)
- Interpolated Scroll Rates (`#SPEEDS`)
- Fake Regions (`#FAKES`)
- Combo Multipliers (`#COMBOS`)

An exception is made for events which `GrooveAuthor` adds to new songs by default. These include:
- 1x Scroll Rate at row 0.
- 1x/1x Combo Multipliers at row 0.
- 1x/0rows Interpolated Scroll Rate at row 0.

## Saving `ssc` Files

Stepmania will ignore negative stops when they are present in `ssc` files. `GrooveAuthor` treats the presence of negative stops in `ssc` files as an error when saving. Negative stops can be converted to warps for compatibility with the `ssc` format by selecting `Edit` > `Convert All` > `Negative Stops to Warps`.