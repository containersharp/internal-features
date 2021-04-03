$VER=$args[0] # || $1

if(-not $VER){
    $VER='dev'    
}

dotnet clean

cd SharpCR.Features.CloudStorage
dotnet build -c Release -r linux-x64

cd ..
cd SharpCR.Features.ReadOnly
dotnet build -c Release -r linux-x64

cd ..
cd SharpCR.Features.SyncIntegration
dotnet build -c Release -r linux-x64

cd ..
docker build --build-arg "BASE_IMG_TAG=$VER" -t "jijiechen-docker.pkg.coding.net/sharpcr/apps/sharpcr-registry-internal:$VER" .
docker push "jijiechen-docker.pkg.coding.net/sharpcr/apps/sharpcr-registry-internal:$VER"