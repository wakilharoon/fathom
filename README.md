# fathom

A local-first command-line tool for logging predictions and measuring how well-calibrated you are.

You make confident predictions all the time such as "this ships Friday," "they won't last a year," "it'll rain tomorrow." `fathom` makes recording one a five-second habit, then tells you, over time, whether that confidence is justified: when you say you're 80% sure, are you actually right 80% of the time?

Everything is stored in a single human-readable JSON file on your own machine. Nothing is sent anywhere.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Install

Clone and run from source:

```
git clone https://github.com/wakilharoon/fathom.git
cd fathom
dotnet run -- --help
```

> Until `fathom` is installed as a global tool, type `dotnet run --` wherever the examples below say `fathom`. For example, `dotnet run -- add "..." -c 80`.

## Usage

```
fathom add "Vendor ships by year end" -c 80 --by 2026-12-31
fathom open
fathom resolve 1 yes
fathom score
```

### Commands

- `add "<statement>" -c <0-100> [--by <date>]` — record a prediction with your confidence (0–100%) and an optional date you expect to know the outcome. Dates use your system's local format.
- `open` — list every unresolved prediction, dated or not.
- `due` — list predictions whose resolve-by date has arrived and are still open.
- `resolve <id> <yes|no>` — record whether a prediction came true.
- `unresolve <id>` — reopen a resolved prediction to fix a mistake.
- `cancel <id>` — mark a prediction unanswerable; it leaves your lists and is excluded from scoring.
- `score` — show your accuracy, calibration, and discrimination.

Run `fathom <command> --help` for details and options on any command.

## How scoring works

`fathom score` reports two different things, because good forecasting needs both:

- **Calibration** — when you say X%, does X% actually happen? If your "70%" predictions come true about 70% of the time, you're calibrated there. If they come true 55% of the time, you're overconfident.
- **Discrimination** — do your confidence levels actually separate what happens from what doesn't? Someone who assigns "60%" to everything can be perfectly calibrated yet useless; discrimination is what catches that.

It also shows your **Brier score**, a single number summarizing accuracy: 0 is perfect, 0.25 is no better than a coin flip, 1 is confidently wrong every time.

The tool withholds a confident verdict until you have enough resolved predictions to judge fairly, so early numbers are shown but not over-interpreted.

## Where your data lives

A single JSON file in your per-user application data folder:

- **Windows:** `%LOCALAPPDATA%\fathom\predictions.json`
- **macOS:** `~/Library/Application Support/fathom/predictions.json`
- **Linux:** `~/.local/share/fathom/predictions.json`

It's plain, indented JSON — safe to read, edit by hand, back up, or commit to your own private git repository. Dates are stored in unambiguous ISO format, so a file created on one machine reads correctly on any other.

## License

MIT — see [LICENSE](LICENSE).
