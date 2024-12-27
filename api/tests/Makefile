restore:
	dotnet restore
build: restore
	dotnet build
test: build 
	dotnet test
testV: build
	dotnet test --logger:"console;verbosity=detailed"
