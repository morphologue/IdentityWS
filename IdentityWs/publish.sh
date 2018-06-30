#!/bin/bash
# This script publishes the project to pub. The Dockerfile copies from there.

PUBLISHDIR=pub
rm -rf $PUBLISHDIR
dotnet publish -c Release -o $PUBLISHDIR
