Git Migration Tools for Bugzilla
--------------------------------

This repo contains tools for the Bugzilla [bzr-git migration][].


FastImportRewriter.cs
=====================

This script is intended to be used during the initial export/import.

Bugzilla has been using bzr commit properties to store info from
corresponding Bugzilla bugs.  In order to preserve this data when
doing the initial migration from bzr to git, one needs to run

    bzr fast-export --no-plain

However, since git doesn't have the concept of commit properties,
piping this data into `git fast-import` results in errors.  We can,
however, translate the fast-export output by storing commit properties
in the git commit messages.  Even better, we can check if the bug
number is already present in the commit message and only add the bug
info if it isn't.

FastImportRewriter.cs is a C# script that does just this.  It is based
on one given in [a blog post][] by David Roth that deals with this
exact problem.  It only required a few modifications, so I didn't
bother translating it into another language.  It runs fine under mono
on Linux: run `mcs FastImportRewriter.cs` to compile, and `mono
FastImportRewriter.exe` to run.  It accepts input on stdin and
produces translated output on stdout.

These are the full steps to migrate trunk (which maps to master in git
terminology), starting from scratch:

    cd /tmp
    bzr branch https://bzr.mozilla.org/bugzilla/trunk/
    cd trunk
    git init
    bzr fast-export --git-branch=master --no-plain | mono /path/to/FastImportRewriter.exe | git fast-import
    rm -rf .bzr
    git reset --hard

At this point you can create a remote repo and push to it:

    git remote add origin <remote URL>
    git push origin master

You can follow the same steps for other branches.  Since git branches
aren't tightly related to each other, unlike it other VCSes, you can
start from scratch with another bzr branch and just put to a new
branch in the same destination repo.


git-to-bzr.pl
=============

This Perl script, based on the previous [bzr-to-cvs.pl][] script,
keeps a bzr repo in sync with a git repo.  It's unidirectional, from
git to bzr.  It's designed to be run periodically, e.g. from cron.  It
needs to be run once per branch like so:

    ./git-to-bzr.pl --from <source branch> --to <dest branch>

<source branch> is the name of the git branch which will be mirrored,
and <dest branch> is the end part of the destination bzr branch URL.
These will normally be the same, except for the master branch, which
translates to trunk on bzr.  The source and destination repos are
hardcoded in the script, so given a source and destination branch of
"4.2", the script would mirror from the git branch named "4.2" at
git://git.mozilla.org/bugzilla/bugzilla.git to the bzr branch at
https://bzr.mozilla.org/bugzilla/4.2/.

No bzr commit properties are created, since this concept doesn't exist
in git.

The -v option can be given to see some command output and other messages.


[bzr-git migration]: https://wiki.mozilla.org/Bugzilla:Migrating_to_git
[a blog post]: http://www.fusonic.net/en/blog/2013/03/26/migrating-from-bazaar-to-git/
[bzr-to-cvs.pl]: http://bzr.mozilla.org/bzr-plugins/bzr-to-cvs
