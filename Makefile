.PHONY: test

test:
	dotnet test SEA.Infisical.Configuration.sln --disable-build-servers -m:1 -p:GeneratePackageOnBuild=false
