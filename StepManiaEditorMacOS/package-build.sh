#!/bin/bash

set -e

APP_NAME="GrooveAuthor"
PROJECT_DIR=$(dirname "$0")
PROJECT="$PROJECT_DIR/StepManiaEditorMacOS.csproj"
BIN_DIR="$PROJECT_DIR/bin"
RELEASES_DIR="$PROJECT_DIR/../Releases"
TEMP_DIR="$RELEASES_DIR/macos-temp"

# Use ad-hoc signature.
SIGNING_IDENTIFIER="-"

# Parse the version.
echo "Parsing version."
VERSION=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" $PROJECT)
echo "Parsed version: $VERSION"

# Delete any existing state.
echo "Deleting bin and temp directories."
rm -rf "$BIN_DIR" "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

# Build and sign the app for a given architecture.
build_and_sign() {
    ARCH=$1
    OUTPUT_DIR="$TEMP_DIR/$ARCH"

    # Clean the project each time.
    echo "Cleaning project."
    dotnet clean $PROJECT
    rm -rf "$BIN_DIR" 

    echo "Building for $ARCH."
    dotnet build -c Release -r $ARCH --self-contained true -o "$OUTPUT_DIR"

    APP_PATH="$OUTPUT_DIR/$APP_NAME.app"

    echo "Signing dylibs for $ARCH."
    find "$APP_PATH" -name "*.dylib" -exec codesign --force --sign "$SIGNING_IDENTIFIER" {} \;

    echo "Signing app bundle for $ARCH."
    codesign --force --sign "$SIGNING_IDENTIFIER" --deep --verbose "$APP_PATH"
}

# Build for both architectures.
build_and_sign "osx-x64"
build_and_sign "osx-arm64"

# Create universal binary.
echo "Creating universal binary."
ARM_APP="$TEMP_DIR/osx-arm64/$APP_NAME.app/Contents/MacOS/$APP_NAME"
X64_APP="$TEMP_DIR/osx-x64/$APP_NAME.app/Contents/MacOS/$APP_NAME"
UNIVERSAL_APP="$TEMP_DIR/universal/$APP_NAME.app"

# Copy the arm64 app as the base.
mkdir -p "$TEMP_DIR/universal/"
cp -R "$TEMP_DIR/osx-arm64/$APP_NAME.app" "$UNIVERSAL_APP"

# Create a universal binary.
lipo -create -output "$UNIVERSAL_APP/Contents/MacOS/$APP_NAME" "$ARM_APP" "$X64_APP"

# Sign the universal binary.
echo "Signing universal app bundle."
find "$UNIVERSAL_APP" -name "*.dylib" -exec codesign --force --sign "$SIGNING_IDENTIFIER" {} \;
codesign --force --sign "$SIGNING_IDENTIFIER" --deep --verbose "$UNIVERSAL_APP"

# Create a dmg.
echo "Creating dmg."
DMG_PATH="$RELEASES_DIR/$APP_NAME-v$VERSION-mac-universal.dmg"
hdiutil create -volname "$APP_NAME" -srcfolder "$UNIVERSAL_APP" -ov -format UDZO "$DMG_PATH"

# Cleanup.
rm -rf $TEMP_DIR

echo "Done."