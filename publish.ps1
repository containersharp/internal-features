cd SharpCR.Features.CloudStorage
dotnet build -c Release -r linux-x64

cd ..
cd SharpCR.Features.CloudStorage
dotnet build -c Release -r linux-x64

docker build -t "sharpcr-registry-internal:1.0.0" .