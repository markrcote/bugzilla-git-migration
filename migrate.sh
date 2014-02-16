#!/bin/bash

# Exports all relevant Bugzilla and BMO from Bazaar repos at bzr.mozilla.org
# to git repos at git.mozilla.org using the tools in this git repository.
# This script should be executed from the same directory as the other
# migration tools.

SRC_REPO_PREFIX="https://bzr.mozilla.org"
DEST_REPO_PREFIX="ssh://gitolite3@git.mozilla.org"
TOOLSDIR=`pwd`
DEBUG=0

function migrate_branch
{
  REPOROOT=$1
  BRANCH=$2
  if [ "$3" == "" ]
  then
    DEST_BRANCH=$BRANCH
    if [ $BRANCH == "trunk" ]
    then
      DEST_BRANCH="master"
    fi
  else
    DEST_BRANCH=$3
  fi

  cd $WORKDIR

  echo "Migrating $REPOROOT/$BRANCH to $DEST_BRANCH..."

  rm -rf bzr

  bzr branch $REPOROOT/$BRANCH bzr
  if [ "$?" != "0" ]
  then
    echo "Failed to check out $BRANCH!"
    exit 1
  fi

  mv git/.git bzr/
  cd bzr

  bzr fast-export --git-branch=$DEST_BRANCH --no-plain > ../$BRANCH.out
  if [ "$?" != "0" ]
  then
    echo "Failed to export!"
    exit 2
  fi

  cat ../$BRANCH.out | mono $TOOLSDIR/FastImportRewriter.exe | git fast-import --quiet
  STUFF=$?
  if [ "$STUFF" != "0" ]
  then
    echo "Weird return code from import! Code $STUFF."
  fi

  cd ..
  mv bzr/.git git/

  cd git
  git checkout -f $DEST_BRANCH

  find . -path ./.git -prune -o -type f -printf "%p %m" | sort > ../git-ls.txt
  find . -path ./.git -prune -o -type f -print | sort | xargs md5sum > ../git-md5s.txt
  cd ../bzr
  find . -path ./.bzr -prune -o -type f -printf "%p %m" | grep -v ' ^./.gitrev ' | sort > ../bzr-ls.txt
  find . -path ./.bzr -prune -o -type f -print | grep -v '^./.gitrev$' | sort | xargs md5sum > ../bzr-md5s.txt

  cd ..
  echo "Diff of file metadata:"
  diff bzr-ls.txt git-ls.txt
  echo "Diff of md5 sums:"
  diff bzr-md5s.txt git-md5s.txt
  echo "End of diffs."

  if [ "$DEBUG" == "0" ]
  then
    cd git
    git fetch git.m.o
    git push git.m.o $DEST_BRANCH
    cd ..
  fi

  echo "Cleaning up."
  mv git/.git .
  rm -rf git
  mkdir git
  mv .git git/
}

function setup
{
  DEST_REPO_PATH=$1
  WORKDIR=`mktemp -d -q /tmp/bzmigrate.XXXX`

  echo "Working in $WORKDIR."

  cd $WORKDIR

  mkdir git
  cd git
  git init
  git remote add git.m.o $DEST_REPO_PREFIX$DEST_REPO_PATH
  cd ..
}

function migrate_repo_branches
{
  SRC_REPO_PATH=$1
  DEST_REPO_PATH=$2
  BRANCHES=$3

  setup $DEST_REPO_PATH

  for b in $BRANCHES
  do
    migrate_branch $SRC_REPO_PREFIX$SRC_REPO_PATH $b
  done
}

function migrate_repo_single_branch
{
  SRC_REPO_PATH=$1
  DEST_REPO_PATH=$2
  SRC_BRANCH=$3
  DEST_BRANCH=$4

  setup $DEST_REPO_PATH

  migrate_branch $SRC_REPO_PREFIX$SRC_REPO_PATH $SRC_BRANCH $DEST_BRANCH
}

echo "Starting migration."

START=`date +%s`

echo "Migrating Bugzilla core."

migrate_repo_branches "/bugzilla" "/bugzilla/bugzilla.git" "2.14 2.16 2.18 2.20 2.22 3.0 3.2 3.4 3.6 4.0 4.2 4.4 sightings trunk"

echo "Migrating Bugzilla qa extension."

migrate_repo_branches "/bugzilla/qa" "/bugzilla/qa.git" "2.20 2.22 3.0 3.2 3.4 3.6 4.0 4.2 4.4 cvs"

echo "Migrating BMO core."

migrate_repo_branches "/bmo" "/webtools/bmo/bugzilla.git" "3.0 3.2 3.4 3.6 4.0 4.0-dev 4.2 4.2-dev"

echo "Migrating BMO qa extension."

migrate_repo_branches "/bmo/qa" "/webtools/bmo/qa.git" "3.6 4.0 4.2"

echo "Migrating extensions."

TRUNK_EXTENSIONS="Browse browserid DescribeUser Developers ExtraValues GNOME PatchReport ProductInterests profanivore requestwhiner sitemap StockAnswers sync typesniffer vcs WeeklyBugSummary"

for e in $TRUNK_EXTENSIONS
do
  migrate_repo_single_branch "/bugzilla/extensions/$e" "/bugzilla/extensions/$e.git" "trunk" "master"
done

BRANCH_EXTENSIONS="cannedcomments rest trackingflags"

for e in $BRANCH_EXTENSIONS
do
  migrate_repo_single_branch "/bugzilla/extensions" "/bugzilla/extensions/$e.git" $e "master"
done

migrate_repo_branches "/bugzilla/extensions/InlineHistory" "/bugzilla/extensions/InlineHistory.git" "1.0 1.1 1.2 1.3 1.4 1.5 trunk"
migrate_repo_branches "/bugzilla/extensions/securemail" "/bugzilla/extensions/securemail.git" "3.2 3.6 4.0"
migrate_repo_branches "/bugzilla/extensions/splinter" "/bugzilla/extensions/splinter.git" "4.0 4.2"
migrate_repo_branches "/bugzilla/extensions/testopia" "/bugzilla/extensions/testopia.git" "1.0-bugzilla-2.20 1.2-bugzilla-2.22 2.1 2.2-bugzilla-3.2 trunk"

echo "Migrating misc."

MISC="active-installs bugzilla-bugbot build landfill tinderbox-bugbot tinderbox-client"

for m in $MISC
do
  migrate_repo_single_branch "/bugzilla/misc" "/bugzilla/misc/$m.git" $m "master"
done

END=`date +%s`

echo "Migration finished!  Elapsed time: $(($END-$START)) seconds."
