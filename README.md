# LookUp — Bitcoin Message Scanner

**LookUp** is a free and open-source .NET server that connects to your local **Bitcoin Knots** node via RPC, scans blockchain transactions for embedded messages (stored in `OP_RETURN` scripts), and saves them into a **PostgreSQL** database.  

# Build From Source Code

## Requirements

- [Git](https://git-scm.com/downloads)
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Bitcoin Knots](https://bitcoinknots.org/)  

## Setup

1. **Clone the repository:**
```sh
   git clone https://github.com/NotesOnBlockchain/LookUp.Core.git
   cd LookUp
   dotnet build && dotnet run (to generate Config.json)
```

2. Install EF Core
```sh
	dotnet tool install --global dotnet-ef --version 9.*
```
   
3. Configure your environment:
Update {appdata}/LookUp/Backend/Config.json:
```sh
{
  "Network": "Main",
  "BitcoinRpcConnectionString": "CONNECTION:STRING",
  "MainNetBitcoinCoreRpcEndPoint": "http://localhost:8332",
  "TestNetBitcoinCoreRpcEndPoint": "http://localhost:48332",
  "RegTestBitcoinCoreRpcEndPoint": "http://localhost:18443",
  "SQLConnectionString": "Server=[serverName];Host=localhost;Port=5432;Database=[DatabaseName];User ID=[PostgreSQL_Username];Password=[PostgreSQL_Password]"
}
```

4. Create your Database then apply database migrations:

```sh
dotnet ef database update
```
5. Launch Bitcoin Knots then start the server:

```sh
dotnet run
```

# Example OP_RETURN

If a transaction contains:

```sh
OP_RETURN 48656c6c6f20576f726c64
```

The decoded message will be:
```sh
"Hello World"
```