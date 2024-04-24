# VL.Audio
Record, play, filter, synthesize and analyse sound.

For use with vvvv, the visual live-programming environment for .NET: http://visualprogramming.net

## Getting started
- Ships with vvvv
- Usage examples and more information are included in the pack and can be found via the [Help Browser](https://thegraybook.vvvv.org/reference/hde/findinghelp.html)

## Audio driver selection and configuration
![VL.Audio.UI Settings UI](https://raw.githubusercontent.com/vvvv/VL.Audio/main/.github/images/ConfigurationUI.png)

As soon as VL.Audio is referenced in a document, the Settings UI is available via `Quad -> Extensions -> VL.Audio.Configuration` or <span class="keyseq"><kbd>Alt</kbd><kbd>C</kbd></span>. Here you can choose between the system default WASAPI driver or any installed ASIO driver and configure its inputs and outputs. Changing the sample rate for the WASAPI driver is only possible via the Windows Sound Settings!

You can also offer this UI in exported applications, see "HowTo Use ConfigurationUI in your patch".

As an alternative to a UI based driver selection have a look at the `DriverSettings [Audio]` node.

### ASIO driver options
In case you want to use an ASIO driver with your soundcard that doesn't come with dedicated ASIO drivers, here are some options:
* [FlexASIO](https://github.com/dechamps/FlexASIO/releases) and [FlexASIO GUI](https://github.com/flipswitchingmonkey/FlexASIO_GUI/releases)
* [ASIO4All](http://www.asio4all.org)
* [FL Studio ASIO](https://www.image-line.com/fl-studio-learning/fl-studio-online-manual/html/envsettings_audio.htm#FLStudioASIO) as included with the free download of [FL Studio](https://www.image-line.com/fl-studio-download)

### Related useful tools
* [Dante Via](https://www.audinate.com/products/software/dante-via) Versatile Audio Networking 
* [Virtual Audio Cable](https://vb-audio.com/Cable/index.htm)

## Contributing
- Report issues on [the vvvv forum](https://discourse.vvvv.org/c/vvvv-gamma/28)
- For custom development requests, please [get in touch](mailto:devvvvs@vvvv.org)
- When making a pull-request, please make sure to read the general [guidelines on contributing to vvvv libraries](https://thegraybook.vvvv.org/reference/extending/contributing.html)

## Credits
Based on [NAudio](https://github.com/naudio/NAudio).
