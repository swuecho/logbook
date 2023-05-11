version=0.5.7
local_registry=192.168.0.100:5555
aliyun_registry_bestqa=registry.cn-shanghai.aliyuncs.com/bestqa
github_pkg_registry_surveyresearch=docker.pkg.github.com/swuecho/surveyresearch

build:
	docker build -t echowuhao/notepad_frontend -f Dockerfile .
	docker tag  echowuhao/notepad_frontend $(local_registry)/notepad_frontend:$(version)
	docker push  $(local_registry)/notepad_frontend:$(version)
	docker tag  echowuhao/notepad_frontend $(aliyun_registry_bestqa)/notepad_frontend:$(version)
	docker push  $(aliyun_registry_bestqa)/notepad_frontend:$(version)
