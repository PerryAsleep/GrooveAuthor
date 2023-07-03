<img src="StepManiaEditor/Content/logo.svg" width="100%"/>

# GrooveAuthor

`GrooveAuthor` is an open-source editor for authoring [StepMania](https://www.stepmania.com/) charts.

[<img src="StepManiaEditor/docs/screenshot-01.png" width="100%"/>](StepManiaEditor/docs/screenshot-01.png)

## Features

- Open source.
- Support for all major dance game layouts including [StepManiaX](https://stepmaniax.com/) layouts.
- Robust autogen functionality. All the functionality from [StepManiaChartGenerator](https://github.com/PerryAsleep/StepManiaChartGenerator) is available in-editor.
- Accurate `ssc` scroll rate rendering.
- Near infinite zooming with 100% accurate waveform rendering and offset compensation.
- Optional automove. Press one key to add a note and advance for quickly writing streams.
- Modern editing conveniences like copy/paste, undo/redo, intuitive mouse and keyboard controls, mini-map scrollbar, etc.

## Installation

`GrooveAuthor` is in-development and does not yet have any releases.

## Building From Source

Building from source requires Windows 10 or greater and Microsoft Visual Studio Community 2022.

1. Clone the repository and init submodules.
	```
	git clone https://github.com/PerryAsleep/GrooveAuthor.git
	git submodule update --init --recursive
	```
2. Open `GrooveAuthor.sln` and build through Visual Studio.
