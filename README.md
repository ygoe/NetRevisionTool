# .NET Revision Tool

Injects the current VCS revision of a working directory in a custom format into a .NET assembly build or just displays it.

See http://unclassified.software/apps/netrevisiontool for further information and download the latest build `NetRevisionTool.exe`.

## Quick reference

To print the help use the following command: `NetRevisionTool.exe /?`

## Introduction

.NET Revision Tool is a small developer’s tool that prints out the current Git or SVN revision info of a working directory. It can automatically write that revision information into your application’s code so that it's compiled right into it. This works for .NET solutions written in C# and VB.NET using the regular Visual Studio project structure.

Currently the following VCS (version control system) are supported:

* Git
* Subversion

More systems can easily be added in the code.

## Why?

Every bigger-than-small application has a version number that the user can query in some form of About dialog window. If you release often and don’t want to manage [semantic version numbers](http://semver.org/) like major.minor.patch (as for .NET Revision Tool itself), you might just use the Git or SVN revision identifier or commit time as version number for your program.

By automating the copying of that revision ID into the application source code, you can avoid forgetting that update. Also, possible keyword replacing features of Git/SVN itself do not play very well with C#/VB.NET source code in creating a friendly version for that assembly. .NET Revision Tool is optimised for this scenario and can adapt to special wishes.

## Licence and terms of use

This software is released under the terms of the GNU GPL licence, version 3. You can find the detailed terms and conditions in the download or on the [GNU website](http://www.gnu.org/licenses/gpl-3.0.html).
