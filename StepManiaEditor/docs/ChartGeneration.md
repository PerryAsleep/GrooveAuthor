# Chart Generation

Chart Generation is the process of generating new charts from existing charts. This can be used for example to add `dance-double` charts for all the `dance-single` charts in your song, or to add charts for another game type, like SMX or PIU.

![Autogen Tech](autogen-tech.gif "GrooveAuthor can interpret modern tech and instantly make new kinds of charts.")

![Autogen SMX](autogen-smx.gif "GrooveAuthor supports all modern chart types, including StepManiaX.")

## Prerequisites

Chart generation requires the [StepGraph Files](https://github.com/PerryAsleep/StepManiaLibrary/tree/main/StepManiaLibrary/docs/StepGraphs.md#stepgraph-files) for the [ChartTypes](https://github.com/PerryAsleep/StepManiaLibrary/tree/main/StepManiaLibrary/docs/ChartType.md) involved to be loaded. GrooveAuthor loads StepGraph Files based on the `Startup Step Graphs` defined in the `Options`.

![Startup Step Graphs in Options](startup-step-graphs.png "Startup Step Graphs in Options need to be set before generating charts.")

## Generating Single Chart

To start generating a chart select `Autogen` > `Autogen New Chart...` from the menu bar.

![Autogen Menu](chart-generation-autogen-new-chart-menu-bar.png "Generating a chart can be started through the Autogen menu.")

You can also select `Autogen New Chart...` from the `Chart List` window.

![Autogen New Chart through Chart List](chart-generation-chart-list-autogen-new-chart-button.png "Generating a chart can be started through the Chart List window.")

You can also right click a chart in the `Chart List` window and select the `Autogen New Chart From...` item.

![Autogen New Chart through Context Menu](chart-generation-chart-list-autogen-new-chart-context-menu.png "Generating a chart can be started through a context menu.")

All of these options will present the `Autogen Chart` window.

![Autogen Chart Window](chart-generation-autogen-chart-window.png "The Autogen Chart Window.")

`Source Chart` is the chart to use as input for generating the new chart.

![Source Chart](chart-generation-autogen-chart-source-chart.png "The Source Chart is the input chart for generation.")

`Expression` specifies which [Expressed Chart Config](ExpressedChartConfigs.md) to use for parsing the `Source Chart`.

![Expression](chart-generation-autogen-chart-expression.png "Expression specifies which Expressed Chart Config to use.")

`New Chart Type` specifies the [ChartType](https://github.com/PerryAsleep/StepManiaLibrary/tree/main/StepManiaLibrary/docs/ChartType.md) to generate.

![New Chart Type](chart-generation-autogen-chart-new-chart-type.png "New Chart Type specifies the chart type to generate.")

`Config` specifies which [Performed Chart Config](PerformedChartConfigs.md) to use for positioning steps in the new chart.

![Config](chart-generation-autogen-chart-performed-chart-config.png "Config specifies which Performed Chart Config to use.")

Click the Autogen button to generate a new chart.

![Autogen Button](chart-generation-autogen-chart-autogen-button.png "Click the Autogen button to generate a new chart.")

Your new chart will show up in the `Chart List` and `GrooveAuthor` will switch focus to the new chart.

![Autogen Results](chart-generation-autogen-chart-results.png "The new chart is visible in the Chart List window.")

## Generating Multiple Charts

To start generating multiple charts select `Autogen` > `Autogen New Set of Charts...` from the menu bar.

![Autogen Menu](chart-generation-autogen-new-charts-menu-bar.png "Generating charts can be started through the Autogen menu.")

This will open the `Autogen Charts` window.

![Autogen Charts Window](chart-generation-autogen-charts-window.png "The Autogen Charts Window.")

`Source Type` specifies which [ChartType](https://github.com/PerryAsleep/StepManiaLibrary/tree/main/StepManiaLibrary/docs/ChartType.md) to use as input. For example if your song has four `dance-single` charts and `Dance Single` is used as the `Source Type`, four new charts will be generated.

![Source Type](chart-generation-autogen-charts-source-type.png "Source Type identifies the input charts for generation.")

`New Type` specifies the [ChartType](https://github.com/PerryAsleep/StepManiaLibrary/tree/main/StepManiaLibrary/docs/ChartType.md) of the charts to generate.

![New Type](chart-generation-autogen-charts-new-type.png "New Type specifies the chart type of the charts to generate.")

`Config` specifies which [Performed Chart Config](PerformedChartConfigs.md) to use for positioning steps in the new charts. When generating multiple charts each source chart's [Expressed Chart Config](ExpressedChartConfigs.md) is used. See [Configuring Expressed Chart Configs Per Chart](ExpressedChartConfigs.md#configuring-expressed-chart-configs-per-chart).

![Config](chart-generation-autogen-charts-performed-chart-config.png "Config specifies which Performed Chart Config to use.")

Click the Autogen button to generate new charts.

![Autogen Button](chart-generation-autogen-charts-autogen-button.png "Click the Autogen button to generate new charts.")

Your new charts will show up in the `Chart List` and `GrooveAuthor` will switch focus to a new chart.

![Autogen Results](chart-generation-autogen-charts-results.png "The new charts are visible in the Chart List window.")

## Bulk Generation

`GrooveAuthor` operates on one song at a time, however all the chart generation functionality it offers is available in [StepManiaChartGenerator](https://github.com/PerryAsleep/StepManiaChartGenerator), which can autogenerate charts for an arbitrary number of songs with one click. `StepManiaChartGenerator` allows you to apply rules globally, or target rules to individual songs or packs with pattern matching. With some simple configuration it is easy to add missing charts for every song on your setup. It is highly recommended to use `StepManiaChartGenerator` instead of `GrooveAuthor` for doing bulk chart generation.