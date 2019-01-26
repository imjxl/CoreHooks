# CoreHooks  

目前的功能只是为了进行自动部署gitpages到自己服务器，通过订阅push事件进行代码拉取。

### 具体用法  

⓵首先部署该项目到自己的服务器，通过Nginx进行反代，默认的访问路径为*域名/api/github/githook*。  

⓶然后设置配置文件中的*SecretKey*，可以是任意值，需要在后面用，*giturl*为自己的git pages项目git地址，*clonePath*为要要克隆的目的地址，我的是放到了/usr/share/nginx/html/下，因为还会生成项目文件夹，所以还要在nginx的配置文件中更改root path。  

⓷然后进入自己的github pages项目，点击最右边的Settings按钮，点击Options中的Webhooks，点击Add webhook，
按照接下来的配置进行填写即可。