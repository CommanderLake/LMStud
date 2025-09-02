Copyright (c) 2025 CommanderLake
All rights reserved.

# 🦙 LM Stud – Local LLMs Minus the Lard

> **TL;DR**  
> A WinForms chat client for `llama.cpp`, `whisper.cpp` and your questionable life choices.  
> **Zero Electron. Zero telemetry. Zero regrets.**

---

## Features

| ☑️ | What It Does |
| --- | --- |
| ✅ | Launches in milliseconds—your GPU blinks and it’s already chatting. |
| ✅ | Edit / regenerate / yeet messages. |
| ✅ | Shows the model’s “thinking” stream (fun for prompt nerds). |
| ✅ | Drag-drop files → instant code blocks. |
| ✅ | One-click Hugging Face search & download. |
| ✅ | **Built-in Google Search + webpage fetch** (super-visible setup below). |
| ✅ | Optional speech I/O with `whisper.cpp`—talk smack to your computer. |
| ✅ | Tiny memory footprint—smaller than RGB keyboard driver. |
| ✅ | Model API handler for remote endpoints. |
| ✅ | Dialectic mode with dual samplers for side-by-side debates. |

---

## Google Search – **READ ME FIRST** ⚠️
<details>

```text
1)  Grab an API key
    https://console.cloud.google.com/apis/dashboard
    → new project → enable “Custom Search API” → copy the key.

2)  Create a Search Engine ID
    https://programmablesearchengine.google.com/controlpanel/overview
    → “Add” → “Search the entire web” → grab the cx ID.

3)  Paste both values in  Settings → Google Search Tool.
    Congrats—~100 free queries per day. Abuse responsibly.
```
</details>

---

## Screenshots

|             Chat Tab            |               Settings Tab              |              Models Tab             |                Hugging Face Tab               |
| :-----------------------------: | :-------------------------------------: | :---------------------------------: | :-------------------------------------------: |
| ![Chat](./screenshots/Chat.PNG) | ![Settings](./screenshots/Settings.PNG) | ![Models](./screenshots/Models.PNG) | ![Huggingface](./screenshots/Huggingface.PNG) |

---

## Quick-Start Build

### Build llama.cpp
```text
1. Install Visual Studio with C# and C++ development tools
2. Clone https://github.com/ggml-org/llama.cpp or download the source code of the latest release
3. Open "x64 Native Tools Command Prompt for VS <version>", cd to the llama.cpp folder and run "mkdir build && cd build"
4. `cmake .. -DGGML_NATIVE=OFF -DGGML_BACKEND_DL=ON -DGGML_AVX2=ON -DGGML_BMI2=ON -DGGML_CUDA=ON -DGGML_CUDA_F16=ON -DLLAMA_CURL=OFF -DLLAMA_ALL_WARNINGS=OFF -DLLAMA_BUILD_TESTS=OFF -DLLAMA_BUILD_TOOLS=OFF -DLLAMA_BUILD_EXAMPLES=OFF -DLLAMA_BUILD_SERVER=OFF`
5. Open build\llama.cpp.sln with visual studio and build the Release version
```
### Copy files
```text
1. Clone https://github.com/CommanderLake/LMStud
2. Adjust "VC++ Directories" in the Stud project so the paths reflect where you cloned llama.cpp and whisper.cpp to
3. From build\bin\Release copy all .dll files to <LMStud solution folder>\LM Stud\bin\x64\Release
4. If you wish to use the Debug version follow the last 2 steps but replace "Release" with "Debug", i use Release for CUDA 10.2 and ReleaseCUDA12 for the CUDA 12.8 build
5. If you want whisper.cpp for voice input the build steps are similar to llama.cpp, clone https://github.com/ggml-org/whisper.cpp copy only whisper.dll to the "LM Stud\bin\x64\Release" folder or whatever your build configuration is
```
### Set up curl for tools and model downloading
```text
1. Set up vcpkg, step "1 - Set up vcpkg": https://learn.microsoft.com/en-us/vcpkg/get_started/get-started
2. From a new visual studio command prompt...
3. vcpkg install SDL2:x64-windows-static
4. vcpkg install curl[openssl]:x64-windows-static
5. Open LM Stud.sln in Visual Studio
6. Adjust "Include Directories" in "VC++ Directories" in the Stud project to your llama.cpp, whisper.cpp and vcpkg locations
7. Build LM Stud
```

---

## Settings Cheat-Sheet

| Section                | Knobs & Dials                                                  |
| ---------------------- | -------------------------------------------------------------- |
| **CPU Params / Batch** | Generation threads, batch threads.                             |
| **Common**             | Context size, GPU layers, temperature, tokens to generate.     |
| **Advanced**           | NUMA strategy, repeat penalty, top-k/p, batch size.            |
| **Voice**              | Model picker, wake word, VAD, frequency threshold, GPU toggle. |
| **Tools**              | Enable Google Search (API key + cx), enable webpage fetch.     |

---

## Recent Releases

- **R37** – Added dual samplers for dialectic mode and switched the UI font to Segoe UI Symbol.
- **R36** – Added tooltip for the generation delay setting.
- **R35** – Improved generation lock so Generate no longer waits.
- **R34** – Introduced a model API handler.

---

## License

No license granted. All rights reserved.  
Viewing and forking on GitHub are permitted under GitHub’s Terms of Service.  
No other rights are granted; any use, redistribution, or modification outside GitHub requires prior written permission.

### Disclaimer

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,  
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE and NON-INFRINGEMENT, TO THE MAXIMUM EXTENT PERMITTED BY LAW.
