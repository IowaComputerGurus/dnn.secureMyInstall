# DNN Secure My Install

__ Use At Own Risk __

This process is provided to help individuals secure their DNN installation, however, due to the nature of the changes it makes, it could result in adverse behaviors

## Overview

This module is a single-use per DotNetNuke installation utility module that will make a number of security enhancing modifications to your "out of the box" DotNetNuke installation.  The following items are modified by the module in a structured, safe manner.

* Machine Keys are Updated - To ensure any previous breach doesn't allow users to still create encrypted values.
* User Passwords are converted from Encrypted -> Hashed.  This improves the security of the passwords by not allowing them to be retreived.
* Updates the web.config to disable "Password Retrieval" and sets up the system to send the user a random password in the situation where they have forgotten their password.

Using this process you can easily update your installation and all users within the installation following the simple two phase approach that we have completely documented.

## DNN Version Compatibility 

This function works for sites running DNN 4.7.0 - 5.x.  Changes are necessary to support changes in the 6.x - 9.x platforms.

## Documentation

Within the Repository we have an overview document that shows how to utilize this module.

