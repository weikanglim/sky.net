#!/bin/bash
# Install dotnet
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
sudo sh -c 'echo -e "[packages-microsoft-com-prod]\nname=packages-microsoft-com-prod \nbaseurl=https://packages.microsoft.com/yumrepos/microsoft-rhel7.3-prod\nenabled=1\ngpgcheck=1\ngpgkey=https://packages.microsoft.com/keys/microsoft.asc" > /etc/yum.repos.d/dotnetdev.repo'
sudo yum install libunwind libic -y
sudo yum install dotnet-sdk-2.0.0 -y

# Install deploy key for git
rsa=~/.ssh/id_gitlab_uiuc

if [ ! -d ~/.ssh ]; then
    mkdir ~/.ssh
fi

cat >$rsa <<EOL
YOUR KEY HERE
EOL

if [ ! -f ~/.ssh/config ]; then
    touch ~/.ssh/config
fi

if ! grep -q "gitlab.engr.illinois.edu" ~/.ssh/config; then
    echo "
    host gitlab.engr.illinois.edu
    HostName gitlab.engr.illinois.edu
    IdentityFile $rsa
    User git
    host gitlab-beta.engr.illinois.edu
    HostName gitlab-beta.engr.illinois.edu
    IdentityFile $rsa
    User git
    " >> ~/.ssh/config
fi

chmod 400 $rsa
chmod 600 ~/.ssh/config

# Clone repository
cd ~/
git clone git@gitlab.engr.illinois.edu:Sky.Net2.0/MP1.git

# Git update from source
# sudo yum install curl-devel expat-devel gettext-devel openssl-devel zlib-devel -y 
# sudo yum install gcc perl-ExtUtils-MakeMaker -y
# mkdir ~/git
# cd ~/git
# wget https://www.kernel.org/pub/software/scm/git/git-2.9.5.tar.gz
# tar xzf git-2.9.5.tar.gz
# cd git-2.9.5
# sudo make prefix=/usr/local/git all
# sudo make prefix=/usr/local/git install