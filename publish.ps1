cd SharpCR.Features.CloudStorage
dotnet build -c Release -r linux-x64

cd ..
cd SharpCR.Features.ReadOnly
dotnet build -c Release -r linux-x64

cd ..
cd SharpCR.Features.SyncIntegration
dotnet build -c Release -r linux-x64

cd ..
docker build -t "jijiechen-docker.pkg.coding.net/sharpcr/apps/sharpcr-registry-internal:1.0.5" .