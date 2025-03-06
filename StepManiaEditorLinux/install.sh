#!/bin/sh
set -eu

if [ $(id -u) -ne 0 ]; then
    echo "Please run as root."
    exit 1
fi

# Back up the existing install.
if [ -d /opt/GrooveAuthor ]; then
    echo "Backing up current GrooveAuthor installation."
    mv /opt/grooveAuthor /opt/grooveAuthor.old
fi

cd "$(dirname "$0")"

# Enure the /opt directory exists.
if ! [ -d /opt ]; then
    echo "Creating /opt directory."
    install -d -m 755 -o root -g root /opt
fi

# Copy the application.
echo "Installing GrooveAuthor."
cp -R --preserve=mode,timestamps grooveAuthor /opt

# Configure the dekstop entry.
echo "Adding desktop entry."
ln -sf /opt/grooveAuthor/GrooveAuthor.desktop /usr/share/applications

# Remove backup.
if [ -d /opt/GrooveAuthor.old ]; then
    echo "Removing previous GrooveAuthor backup."
    rm -rf /opt/grooveAuthor.old
fi

echo "Done."