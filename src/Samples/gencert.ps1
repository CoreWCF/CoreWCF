# Setup Https Certificate a.k.a SSL for the dotnet WCF servers
# https://docs.microsoft.com/en-us/dotnet/core/additional-tools/self-signed-certificates-guide

$secretsId = "corewcf-samples-secrets"
$projName = "NetCoreServer"

if (-not $password) {
    $password = Read-Host -Prompt "pfxPassword"
}

function Get-CsProjPath ($projName) {  
    ( Get-ChildItem -Path "$projName.csproj" -Recurse |
    Select-Object -First 1).FullName 
}

function Set-SSL_NetCoreServer {
    $csProj = Get-CsProjPath $projName

    $certPath = "$env:USERPROFILE\.aspnet\https\$projName.pfx"

    # $secretsPath = "$env:APPDATA\Microsoft\UserSecrets\$secretsId"

    dotnet dev-certs https -ep $certPath -p $password
    dotnet dev-certs https --trust
    dotnet user-secrets init -p $csProj --id $secretsId
    dotnet user-secrets -p $csProj set "Kestrel:Certificates:Development:Password" $password
    dotnet user-secrets -p $csProj set "Kestrel:Certificates:Development:Path" $certPath
}
Set-SSL_NetCoreServer

function Set-SSL_WCF_ServiceHost {
    $passwordsecured = ConvertTo-SecureString $password -AsPlainText -Force
    $DesktopServerPath = Get-CsProjPath "DesktopServer"
    dotnet user-secrets init -p $DesktopServerPath --id $secretsId    

    $certificateObject = New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Certificate2( `
    $certPath, $passwordsecured, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)

    Import-PfxCertificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\Root' -Password $passwordsecured
    $certHash = "certhash=$($certificateObject.Thumbprint)"
    $appid = "appid={1d7985f5-e1b0-4fd4-bdeb-7e28590783da}"

    netsh http add sslcert ipport=127.0.0.1:8443 $certHash $appid certstorename=Root
    netsh http add sslcert hostnameport=localhost:8443 $certHash $appid certstorename=Root
    netsh http add sslcert hostnameport=localtest:8443 $certHash $appid certstorename=Root
#    Netsh http delete sslcert ipport=127.0.0.1:8443
#    Netsh http delete sslcert hostnameport=localhost:8443
#   netsh http add urlacl url=https://+:8443/ user=everyone
}
Set-SSL_WCF_ServiceHost
