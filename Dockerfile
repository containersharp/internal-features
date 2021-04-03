ARG BASE_IMG_TAG=dev
FROM jijiechen-docker.pkg.coding.net/sharpcr/apps/sharpcr-registry:$BASE_IMG_TAG

COPY ./SharpCR.Features.CloudStorage/bin/Release/netcoreapp3.1/linux-x64/SharpCR.Features.CloudStorage.* /app/
COPY ./SharpCR.Features.ReadOnly/bin/Release/netcoreapp3.1/linux-x64/SharpCR.Features.ReadOnly.* /app/
COPY ./SharpCR.Features.SyncIntegration/bin/Release/netcoreapp3.1/linux-x64/SharpCR.Features.SyncIntegration.* /app/