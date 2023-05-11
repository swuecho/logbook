# qa-editor

This is the frontend of logbook editor. The backend is writen in F#.

frontend need to know to backend url.

1. add the url to `.env` (`.env.development`)


## password protect the frontend code

```sh
sudo sh -c "echo -n 'sammy:' >> /etc/nginx/.htpasswd"
```

```sh
sudo sh -c "openssl passwd -apr1 >> /etc/nginx/.htpasswd"
```

https://www.digitalocean.com/community/tutorials/how-to-set-up-password-authentication-with-nginx-on-ubuntu-14-04