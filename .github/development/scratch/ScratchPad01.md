<div align="center">

  <img src="../../logo/RepositoryTemplate-336x220.png" alt="Repository Template">

  <h1>Scratch Pad</h1>
  
</div>

Windows
```
cls && echo "CLEANING..." && dotnet clean && cls && echo "BUILDING..." && dotnet build && cls && echo "TESTING..." && dotnet test && cls && echo "PUBLISHING..." && dotnet publish EchoesOfTheShardveil.Desktop/EchoesOfTheShardveil.Desktop.csproj -c Release -r win-x64 --self-contained
```

MacOS
```
dotnet clean && dotnet build EchoesOfTheShardveil.slnx && dotnet test && dotnet publish EchoesOfTheShardveil.Desktop/EchoesOfTheShardveil.Desktop.csproj -c Release -r osx-arm64 --self-contained
```
