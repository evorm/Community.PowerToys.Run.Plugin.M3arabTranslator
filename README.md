
![icon](Images/icon.png)

# M3arab Translator - A PowerToys Run Plugin

A PowerToys Run plugin that converts Kuwaiti M3arab / Arabizi into Arabic script using the OpenAI Responses API.

![Demo](Images/demo.gif)

## What it does

- Converts Kuwaiti M3arab / Arabizi into Arabic script
- Uses configurable OpenAI instructions from PowerToys settings
- Copies the result to the clipboard when selected
- Lets you configure:
  - OpenAI API key
  - model
  - instructions
  - reasoning effort

## Demo: reasoning tradeoff

The model is not perfectly reliable on difficult slang, profanity, fused words, or ambiguous spellings.

Prefixing a query with `.` temporarily increases reasoning effort by one step for that request only:

- `minimal -> low`
- `low -> medium`
- `medium -> high`
- `high -> high`

This can improve some outputs, but it also adds latency and still does not guarantee perfect accuracy.

![Reasoning tradeoff demo](Images/demo-dot.gif)

## Usage

Typing **kw** will activate M3arab Translator, then you type your query directly after a space (or after a ".").
Pressing tab will select the query and pressing Enter with it selected will copy the result to your clipboard.

### Examples

Open PowerToys Run and type:

    kw shrayik


Temporary reasoning bump for a single query:

    kw . shrayik

## Settings

In PowerToys:

`PowerToys Settings -> PowerToys Run -> Plugins -> M3arab Translator`

**You MUST Configure:**

- `OpenAI API Key`

**You will need an API key from OpenAI.** Look [**below**](#openapi-key-information) for more information.

The following may be configured to your liking but come with default values:

- `OpenAI Model`
- `Instructions`
- `Reasoning Effort`

## Known limitations

- This is not a deterministic transliterator.
- The model can still misread slang, profanity, fused words, or ambiguous Kuwaiti spellings.
- Higher reasoning effort may improve some outputs, but also increases latency.
- Output quality depends heavily on the active instructions and the model being used.

## Installation

### For users

**Pre-Requisites:**

1. Install [PowerToys](https://github.com/microsoft/PowerToys/releases) and enable PowerToys Run in its settings if not already enabled.

**Plugin Installation:**

1. Download the release files.
2. Extract the zip, then copy the plugin folder into your PowerToys Run plugins directory.
3. Restart PowerToys.

Typical user plugin path (paste this into the top bar in File Explorer):

    %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\

![Install Path](Images/plugin-path.png)

## OpenAPI Key Information 

This plugin uses the **OpenAI API**, so you need your **own API key** and **API billing**.    
A normal ChatGPT subscription does **not** cover API usage.

### How to get an API key

1. Go to the [OpenAI API Keys page](https://platform.openai.com/api-keys).
2. Click **Create new secret key**.
3. Copy the key right away and save it somewhere safe.
4. Paste it into this plugin’s settings in PowerToys Run.

OpenAI says API keys are created at the **project** level, and you should keep them private. 

### How to add billing

1. Open the [Billing Overview](https://platform.openai.com/settings/organization/billing/overview).
2. Add your payment details.
3. Buy prepaid credits if you want a fixed spending buffer. **Make sure to turn off auto recharge if you do not intend to automatically pay once it runs out**.

OpenAI supports **prepaid billing**. The minimum prepaid purchase is **$5**. Prepaid credits expire after **1 year** and are **non-refundable**.

### Rough cost for using this plugin

This plugin usually sends a **small GPT-5 nano request**. OpenAI’s published pricing for GPT-5 nano is:

- **Input:** $0.05 per 1 million tokens
- **Output:** $0.40 per 1 million tokens

In normal use, this plugin is usually **very cheap**.

A typical short query is roughly:
- about **150–250 input tokens**
- about **10–40 output tokens**

That works out to roughly:

- **about $0.00001 to $0.00003 per request**
- **about $0.001 to $0.003 for 100 requests**
- **about $0.1 to $0.3 for 10,000 requests**

Those are rough estimates, not guarantees, but the point is simple: a few normal transliteration requests cost basically nothing.

### Important

- Keep your API key private.
- Do **not** post it publicly or commit it to GitHub.
- If the key is leaked, delete it and make a new one from the same API Keys page.

&nbsp;


## DEVELOPERS
### Release contents

A release should include the runtime files only, such as:

- `Community.PowerToys.Run.Plugin.M3arabTranslator.dll`
- `plugin.json`
- `Images/icon.png`
- any required dependency DLLs

Do not ship source files, `bin/`, or `obj/` in the release zip.

## Building

From the project folder:

    dotnet build -c Release

## Source layout

Important project files:

- `Main.cs`
- `plugin.json`
- `Images/icon.png`
- `Community.PowerToys.Run.Plugin.M3arabTranslator.csproj`

Generated folders like `bin/` and `obj/` should not be committed.


## License

MIT