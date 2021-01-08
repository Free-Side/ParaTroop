# ParaTroop

ParaTroop is intended to make collaborative editing for [Sonic Pi](https://sonic-pi.net/) easier. Instead of asking collaborators to install Sonic Pi and the [Troop Client](https://github.com/Qirky/Troop#running-the-troop-client), a single leader can install and run the [Troop Server](https://github.com/Qirky/Troop#running-the-troop-server), register it via ParaTroop, and then collaborators can join and participate via the browser without any local software. Currently audio must be shared via an independent channel such as a Zoom meeting.

## Building ParaTroop

TODO: Document build process.

### Regenerating Documentation

When new versions of sonic-pi are released it is neces

1. Build and install ruby-coreaudio gem
1. Update the sonic-pi subrepo to the most recent version.
1. Build and install sonic-pi server ruby library gem
1. Run SonicPiDocGen
1. Copy output to `ParaTroop.Web/ClientApp/src/sonic-pi-docs.json`

## Running ParaTroop

TODO: Document running locally.

## Deploying ParaTroop

TODO: Document deployment process.