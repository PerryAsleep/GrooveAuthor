# Saving Simfiles

## Simfiles

The files that are created or edited by `GrooveAuthor` that represent songs and charts and are used by Stepmania are typically called simfiles. `GrooveAuthor` supports two kinds of simfiles:
- `sm`: An older, more restrictive format.
- `ssc`: A newer, more permissive format.

> [!NOTE]
>It is highly recommended to use `ssc` files instead of `sm` files.

`ssc` and `sm` files are very similar. The main differences are:
- `sm` files do not support all kinds of events.
- `sm` files require all charts in a song to have identical miscellaneous events.

## Song Timing and Chart Timing

`GrooveAuthor` always uses chart timing. This means that all timing, scroll, and other miscellaneous events like labels, attacks, multipliers, tick counts, and fake regions are always specified per chart. More information about song timing and chart timing and information about how to easily keep chart events in sync can be found in [Song and Chart Timing](SongAndChartTiming.md).

### Synchronizing Events Between Charts

To keep timing, scroll, and other miscellaneous events syncronized between charts the `Song Properties` window exposes an `Apply...` button next to the `Timing Chart` which provides options for quickly synchonizing these events. See [Song and Chart Timing](SongAndChartTiming.md) for more information on the `Timing Chart`.

## Miscellaneous Events

The following chart gives an overview of the kinds of miscellaneous events that are affected by simfile format.

| Event                    | Simfile Name      | SSC Support | SM Support | Indicates Split Timing in Stepmania |
|--------------------------|-------------------|-------------|------------|-------------------------------------|
| Time Signature           | `#TIMESIGNATURES` | X           | X          | X                                   |
| Tempo                    | `#BPMS`           | X           | X          | X                                   |
| Stop                     | `#STOPS`          | X           | X          | X                                   |
| Delay                    | `#DELAYS`         | X           | X          | X                                   |
| Warp                     | `#WARPS`          | X           |            | X                                   |
| Scroll Rate              | `#SCROLLS`        | X           |            | X                                   |
| Interpolated Scroll Rate | `#SPEEDS`         | X           |            | X                                   |
| Fake Region              | `#FAKES`          | X           |            | X                                   |
| Multipliers              | `#COMBOS`         | X           |            | X                                   |
| Tick Counts              | `#TICKCOUNTS`     | X           | X          | X                                   |
| Label                    | `#LABELS`         | X           |            | X                                   |
| Attack                   | `#ATTACKS`        | X           | X          |                                     |
| Pattern                  |                   |             |            |                                     |

## Saving `ssc` Files

`ssc` files are the best format to use as they are the most permissive.

### `ssc` Save Errors and Warnings

Stepmania will ignore negative stops when they are present in `ssc` files as the intended way to achieve the same effect is to use a warp. `GrooveAuthor` treats the presence of negative stops in `ssc` files as an error when saving. Negative stops can be converted to warps for compatibility with the `ssc` format by selecting `Edit` > `Convert All` > `Negative Stops to Warps`.

### Forcing Song Timing

`ssc` files can be forced to use song level timing instead of chart level timing by enabling `Remove Chart Timing` under `File` > `Advanced Save Options`. It is not recommended to enable this unless you have a specific need to. This feature was added to support specific chart contest rules which had strict requirements about file format. If this option is enabled then all events in the Miscellaneous Events table above under `Indicates Split Timing in Stepmania` will be omitted from each chart and the song's `Timing Chart` will be used to specify these events at the song level.

When this option is enabled any discrepancies between these events in the song's charts will be logged as warning. If the descrepancies would results in different timing or scroll rates then an error will be displayed and the chart will fail to save.

## Saving `sm` Files

`sm` files do not support all events and require many kinds of events to be identical across all charts in the song. If your setup is locked to an older version of Stepmania that does not have support for `ssc` files then you may want to save in this format. If your setup does not strictly require use of the `sm` format, then you should use the `ssc` format.

The Miscellaneous Events table above shows which events are supported by the `sm` format.

### `sm` Save Errors and Warnings

When saving a song as an `sm` file, the song's `Timing Chart` will be used to supply all supported miscellaneous events. If any chart has timing data which differs from the `Timing Chart` then an error is displayed and the song will not be saved. This behavior can be overridden by unchecking `Require Identical Timing in SM Files` under `File` > `Advanced Save Options`.

---

If any chart contains events which aren't compatible with the `sm` format, *and those events would produce a chart where note timing would be affected by their absence*, then an error is displayed and the song will not be saved unless those events are removed. `GrooveAuthor` treats the following events as incompatible and will log errors when they are present:
- Warps (`#WARPS`)

Warps can be converted to negative stops for compatibility with the `sm` format by selecting `Edit` > `Convert All` > `Warps to Negative Stops`.

---

Any events which are not supported in the `sm` format but do not affect timing will be treated as warnings and they will be omitted when saving. The events supported in `sm` files depends on the version of Stepmania. `GrooveAuthor` treats the following events as incompatible and will log warnings when they are present:
- Scroll Rates (`#SCROLLS`)
- Interpolated Scroll Rates (`#SPEEDS`)
- Fake Regions (`#FAKES`)
- Combo Multipliers (`#COMBOS`)

An exception is made for events which `GrooveAuthor` adds to new songs by default. These include:
- 1x Scroll Rate at row 0.
- 1x/1x Combo Multipliers at row 0.
- 1x/0rows Interpolated Scroll Rate at row 0.

## Custom Save Data

`GrooveAuthor` persists custom save data into `sm` and `ssc` files that Stepmania safely ignores. This data is required for some functionality like Patterns, sync compensation for assist tick and waveform visuals, and automatic chart generation. This data can be omitted by enabling `Remove Custom Save Data` under `File` > `Advanced Save Options`, but this is not recommended unless you are working under restrictions to file format beyond normal Stepmania requirements.

## Advanced Save Options

There are a number of advanced save options available under `File` > `Advanced Save Options`.

### Require Identical Timing in SM Files

As mentioned above, `sm` files require identical miscellaneous events between all charts, however this option when unchecked allows for saving `sm` files even if their miscellaneous events are not identical. The `Timing Chart`'s events will be used and in the event of any discrepancies betwen charts warnings will be logged instead of errors.

### Remove Chart Timing

If checked then individual charts will have their timing events omitted from their files and instead the events from the song's `Timing Chart` will be used and saved at the song level. The specific events this affects are those under the `Indicates Split Timing in Stepmania` column in the above Miscellaneous Events table. This option has no effect on `sm` files as they are already limited to only using timing data specified at the song level. Under normal circumstances this option is not recommended but if you use simfiles for other applications which struggle with chart timing data or you are working under additional restrictions to file format this option may be useful. 

### Remove Custom Save Data

As mentioned above, `GrooveAuthor` persists custom save data in simfiles that Stepmania safely ignores. This option, if checked, will omit this data when saving. Under normal circumstances it is not recommended to remove this data.

### Anonymize Save Data

Intended for contests which require specific simfile properties to be empty, this option, if chcked, will omit many properties from saved simfiles. The specific events which will be omitted are:
- Song Credit (#`CREDIT`)
- Song Genre (#`GENRE`)
- Song Origin (#`ORIGIN`)
- Song Banner (#`BANNER`)
- Song Background (#`BACKGROUND`)
- Song CD Title (#`CDTITLE`)
- Song Jacket (#`JACKET`)
- Song CD Image (#`CDIMAGE`)
- Song Disc Image (#`DISCIMAGE`)
- Song Preview Video (#`PREVIEWVID`)
- Song Lyrics (#`LYRICSPATH`)
- Chart Name (#`CHARTNAME`)
- Chart Description (#`DESCRIPTION`)
- Chart Credit (#`CREDIT`)
- Chart Style (#`CHARTSTYLE`) 

### Use StepF2 Format for Pump Routine

[StepF2](https://stepf2.blogspot.com/) (a.k.a StepF1) is a popular Stepmania fork with Pump Co-Op emulation, however it uses a simfile format which is not compatible with stock Stepmania and only supports up to four players. This format also does not support rolls, lifts, or per-player mines. If this option is checked then Pump Routine charts will be saved using the StepF2 format.

See also [StepF2 Support](MultiplayerCharts.md#stepf2-support).