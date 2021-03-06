Git Migration Tools for Bugzilla
================================

This repo contains tools for the Bugzilla [bzr-git migration][].

These scripts require git, bzr, the bzr fast-export plugin, perl, and C#/mono.
On Ubuntu, you can install these packages:

* git
* bzr
* bzr-fastimport
* perl
* mono-mcs

migrate.sh
----------

This bash script exports all relevant Bazaar repos at bzr.mozilla.org
to git repos at git.mozilla.org using the other tools in this repo.
Logically related branches, e.g. Bugzilla versions, are created as
different git branches in the same git repo.  Other standalone branches
are exported as just a master branch in a new repo.  Note that a few
git repos will not have the standard master branch, since they have no
associated bzr trunk branch.

A few Bazaar branches are not migrated due to them being irrelevant or
broken.


FastImportRewriter.cs
---------------------

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
-------------

This Perl script, based on the previous [bzr-to-cvs.pl][] script,
keeps a bzr repo in sync with a git repo.  It's unidirectional, from
git to bzr.  It's designed to be run periodically, e.g. from cron.  It
needs to be run once per branch like so:

    ./git-to-bzr.pl --from-repo=<source repo> --from-branch=<source branch> --to-repo=<dest repo> --to-branch=<dest branch>

<source repo> is the local path or URL of the git repo containing
<source branch>, which is the name of the git branch which will be
mirrored. <dest repo> is the path or URL up to but not including the branch
name. <dest branch> is the end part of the destination bzr branch URL.
The two branch names will normally be the same, except for the master branch,
which translates to trunk on bzr.

No bzr commit properties are created, since this concept doesn't exist
in git.

The -v option can be given to see some command output and other messages.
Without it, only errors will be printed.

Before this is first run, you must commit a .gitrev file to each
destination bzr branch that contains the full ID of the last git
commit of that branch.  The script uses this file to determine new
commits, and it updates and commits it automatically.

You can always test this yourself:

* Create a local Bugzilla branch from upstream, e.g. `bzr branch
  https://bzr.mozilla.org/bugzilla/4.2/`.
* Create a local, empty git repo elsewhere via `git init`.
* Perform a migration as described in the section above.
* Edit git-to-bzr.pl to specify paths to your local bzr branch and git
  repo.
* Check in a .gitrev file to your local Bugzilla branch containing the
  ID of the last git commit.
* Make some commits to your git repo.
* Run git-to-bzr.pl with the appropriate branch name(s).

Each commit should be preserved individually.


[bzr-git migration]: https://wiki.mozilla.org/Bugzilla:Migrating_to_git
[a blog post]: http://www.fusonic.net/en/blog/2013/03/26/migrating-from-bazaar-to-git/
[bzr-to-cvs.pl]: http://bzr.mozilla.org/bzr-plugins/bzr-to-cvs
