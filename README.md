# Addressable Dependency Analyzer

Unity Editor utility for analyzing Addressables build layout and identifying local-to-remote asset references.

## Overview

This tool inspects the Addressables build layout and reports when assets in local build paths reference assets in remote build paths. It helps you understand remote dependency relationships in an Addressables project.

## Features

- Parses `BuildLayout` data from an Addressables build layout file
- Maps Addressables groups by GUID to distinguish local and remote groups
- Collects references from local assets to remote assets
- Exposes the dependency map through `RemoteDepGroups`
- Outputs local-to-remote references via Unity Debug logs

## Requirements

- Unity 2018.1 or later
- `com.unity.addressables` package installed

## Installation

1. Copy the `Editor` folder from this repository into the root of your Unity project.
2. Confirm that Addressables is installed and configured.
3. Place the build layout `.json` or `.bin` file in a known path.

## Unity Package Git Installation

To install this tool via Git in your Unity project's `Packages/manifest.json`, add the dependency like this:

```json
{
  "dependencies": {
    "com.unity.addressables": "2.9.1",
    "addressable-dependency-analyzer": "https://github.com/josangjun/addressable-dependency-analyzer.git"
  }
}
```

> Replace `https://github.com/josangjun/addressable-dependency-analyzer.git` with the actual Git repository URL.

To specify a branch or commit, use the following format:

```json
{
  "dependencies": {
    "addressable-dependency-analyzer": "https://github.com/josangjun/addressable-dependency-analyzer.git#main"
  }
}
```

## Usage

Use `AddressablesBuildLayoutAnalyzer` from editor code or a custom menu/window.

Example:

```csharp
var analyzer = new XSystem.Addressable.Analyzer.AddressablesBuildLayoutAnalyzer(buildLayoutPath);
analyzer.PrintLocalToRemoteRefs();
```

Access the dependency map:

```csharp
var remoteDeps = analyzer.RemoteDepGroups;
```

## Project Structure

- `Editor/AddressablesBuildLayoutAnalyzer.cs` - Main analyzer implementation
- `Editor/AddressablesDependencyWindow.cs` - Editor window for dependency visualization
- `Editor/AddressablesStaticRemoteDependencyReporter.cs` - Static reporting helper
- `Editor/AddressGroup.cs` - Addressable group helper

## Notes

This repository is intended as an editor-only utility and should be placed under a Unity `Editor` folder.
