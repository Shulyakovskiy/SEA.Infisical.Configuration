.PHONY: test

test:
	dotnet test MonixOne.Infisical.Configuration.sln --disable-build-servers -m:1 -p:GeneratePackageOnBuild=false
