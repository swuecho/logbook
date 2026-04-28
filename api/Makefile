version=0.0.16
aliyun_registry_bestqa=registry.cn-shanghai.aliyuncs.com/bestqa
local_registry=192.168.0.100:5555
github_pkg_registry_surveyresearch=docker.pkg.github.com/swuecho/surveyresearch
DOTNET ?= dotnet

tag:
	git tag release-v$(version)
	git push origin tag release-v$(version)

build:
	docker build -t $(local_registry)/logbook:$(version) 
	docker push $(local_registry)/logbook:$(version)

push:
	docker tag $(local_registry)/logbook:$(version) $(github_pkg_registry_surveyresearch)/logbook:$(version)
	docker tag $(local_registry)/logbook:$(version) $(aliyun_registry_bestqa)/logbook:$(version)
	docker push $(aliyun_registry_bestqa)/logbook:$(version)

serve:
	$(DOTNET) restore
	$(DOTNET) build
	$(DOTNET) watch run

migrate:
	$(DOTNET) run --project Migrations -- "$(DATABASE_URL)"

flypg:
	# psql   postgres://localhost:15432
	fly proxy 15432:5432 -a swuecho-chat-db 