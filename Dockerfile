FROM jijiechen-docker.pkg.coding.net/sharpcr/apps/sharpcr-registry:1.0.3

COPY ./SharpCR.Features.CloudStorage/bin/Release/netcoreapp3.1/linux-x64/SharpCR.Features.CloudStorage.* /app/
COPY ./SharpCR.Features.SyncIntegration/bin/Release/netcoreapp3.1/linux-x64/SharpCR.Features.SyncIntegration.* /app/