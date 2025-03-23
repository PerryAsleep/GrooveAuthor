#!/bin/bash

set -e

APP_NAME="GrooveAuthor"
PROJECT_DIR=$(dirname "$0")
PROJECT="$PROJECT_DIR/StepManiaEditorMacOS.csproj"
BIN_DIR="$PROJECT_DIR/bin"
RELEASES_DIR="$PROJECT_DIR/../Releases"
TEMP_DIR="$RELEASES_DIR/macos-temp"

# ARM_DIR="$TEMP_DIR/osx-arm64/$APP_NAME.app"
# ARM_APP="$ARM_DIR/Contents/MacOS/$APP_NAME"
# X64_DIR="$TEMP_DIR/osx-x64/$APP_NAME.app"
# X64_APP="$X64_DIR/Contents/MacOS/$APP_NAME"
# UNIVERSAL_DIR="$TEMP_DIR/universal/$APP_NAME.app"
# UNIVERSAL_APP="$UNIVERSAL_DIR"

# Use ad-hoc signature.
SIGNING_IDENTIFIER="-"

# # Function to compare dylib files across architectures to ensure they are consistent.
# check_dylib_consistency() {
#     echo "Checking for missing dylib files..."

#     ARM_DYLIBS=$(find "$ARM_DIR" -name "*.dylib" | sed "s|$ARM_DIR/||")
#     X64_DYLIBS=$(find "$X64_DIR" -name "*.dylib" | sed "s|$X64_DIR/||")

#     MISSING_IN_X64=()
#     MISSING_IN_ARM=()

#     for dylib in $ARM_DYLIBS; do
#         if [[ ! -f "$X64_DIR/$dylib" ]]; then
#             MISSING_IN_X64+=("$dylib")
#         fi
#     done

#     for dylib in $X64_DYLIBS; do
#         if [[ ! -f "$ARM_DIR/$dylib" ]]; then
#             MISSING_IN_ARM+=("$dylib")
#         fi
#     done

#     if [[ ${#MISSING_IN_X64[@]} -ne 0 || ${#MISSING_IN_ARM[@]} -ne 0 ]]; then
#         echo "Mismatched dylibs detected."
#         if [[ ${#MISSING_IN_X64[@]} -ne 0 ]]; then
#             echo "Missing in x86_64 build:"
#             printf ' - %s\n' "${MISSING_IN_X64[@]}"
#         fi
#         if [[ ${#MISSING_IN_ARM[@]} -ne 0 ]]; then
#             echo "Missing in arm64 build:"
#             printf ' - %s\n' "${MISSING_IN_ARM[@]}"
#         fi
#         exit 1
#     fi

#     echo "All dylib files are present in both builds."
# }

# # Function to create universal dylibs from arm64 and x86_64 dylibs.
# create_universal_dylibs() {
#     echo "Merging dylibs..."
#     find "$ARM_DIR" -name "*.dylib" | while read dylib; do
#         RELATIVE_PATH="${dylib#$ARM_DIR/}"
#         ARM_DYLIB="$ARM_DIR/$RELATIVE_PATH"
#         X64_DYLIB="$X64_DIR/$RELATIVE_PATH"
#         UNIVERSAL_DYLIB="$UNIVERSAL_DIR/$RELATIVE_PATH"

#         # Check if already universal
#         if lipo -info "$ARM_DYLIB" | grep -q "x86_64" && lipo -info "$ARM_DYLIB" | grep -q "arm64"; then
#             echo "Already Universal: $RELATIVE_PATH."
#             continue
#         fi

#         # Merge
#         lipo -create -output "$UNIVERSAL_DYLIB" "$ARM_DYLIB" "$X64_DYLIB"
#         echo "Merged:            $RELATIVE_PATH"
#     done
# }

# Function to build and sign the app for a given architecture.
build_and_sign() {
    ARCH=$1
    OUTPUT_DIR="$TEMP_DIR/$ARCH"
    APP_PATH="$OUTPUT_DIR/$APP_NAME.app"

    # Clean the project each time.
    echo "Cleaning project."
    dotnet clean -c Release $PROJECT
    rm -rf "$BIN_DIR"

    # Build.
    echo "Building for $ARCH."
    dotnet build -c Release -r osx-$ARCH --self-contained true -o "$OUTPUT_DIR"

    # Sign.
    echo "Signing dylibs for $ARCH."
    find "$APP_PATH" -name "*.dylib" -exec codesign --force --sign "$SIGNING_IDENTIFIER" {} \;
    echo "Signing app bundle for $ARCH."
    codesign --force --sign "$SIGNING_IDENTIFIER" --deep --verbose "$APP_PATH"

    # Create a dmg.
    echo "Creating dmg for $ARCH."
    DMG_PATH="$RELEASES_DIR/$APP_NAME-v$VERSION-mac-$ARCH.dmg"
    hdiutil create -volname "$APP_NAME" -srcfolder "$APP_PATH" -ov -format UDZO "$DMG_PATH"
}

# Parse the version.
echo "Parsing version."
VERSION=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" $PROJECT)
echo "Parsed version: $VERSION"

# Delete any existing state.
echo "Deleting bin and temp directories."
rm -rf "$BIN_DIR" "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

# Build for both architectures.
build_and_sign "x64"
build_and_sign "arm64"

# Do not attempt to make a universal binary
# For this to work we'd additionally need to handle the dlls which can be architecture-specific.
# It seems better to just make two builds as this keeps dll loading simple, and results in smaller builds.

# # Once both architectures are built, ensure the dylibs for each match so we can make universal variants.
# check_dylib_consistency

# # Create universal binary using arm64 as the base.
# echo "Creating universal binary."
# mkdir -p "$TEMP_DIR/universal/"
# cp -R "$TEMP_DIR/osx-arm64/$APP_NAME.app" "$UNIVERSAL_APP"
# lipo -create -output "$UNIVERSAL_APP/Contents/MacOS/$APP_NAME" "$ARM_APP" "$X64_APP"

# # Create universal binary versions of all the non-universal dylib files.
# create_universal_dylibs

# # Sign the universal binary.
# echo "Signing universal app bundle."
# find "$UNIVERSAL_APP" -name "*.dylib" -exec codesign --force --sign "$SIGNING_IDENTIFIER" {} \;
# codesign --force --sign "$SIGNING_IDENTIFIER" --deep --verbose "$UNIVERSAL_APP"

# # Create a dmg.
# echo "Creating dmg."
# DMG_PATH="$RELEASES_DIR/$APP_NAME-v$VERSION-mac-universal.dmg"
# hdiutil create -volname "$APP_NAME" -srcfolder "$UNIVERSAL_APP" -ov -format UDZO "$DMG_PATH"

# Cleanup.
# rm -rf $TEMP_DIR

echo "Done."