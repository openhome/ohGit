# Defines the build behaviour for continuous integration builds.

import sys

try:
    from ci import (OpenHomeBuilder, require_version)
except ImportError:
    print "You need to update ohDevTools."
    sys.exit(1)

require_version(19)


class Builder(OpenHomeBuilder):
    def clean(self):
        self.msbuild('src/ohGit.sln', target='Clean', configuration=self.configuration)

    def build(self):
        self.msbuild('src/ohGit.sln', target='Build', configuration=self.configuration)

    def publish(self):
        if self.options.auto and not self.platform == 'Linux-x86':
            # Only publish from one CI platform, Linux-x86.
            return
        self.publish_package(
            'ohGit-AnyPlatform-{configuration}.tar.gz',
            'ohGit/ohGit-{version}-AnyPlatform-{configuration}.tar.gz')
