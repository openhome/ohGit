# Defines the build behaviour for continuous integration builds.

import sys

try:
    from ci import (OpenHomeBuilder, require_version)
except ImportError:
    print "You need to update ohDevTools."
    sys.exit(1)

require_version(22)


class Builder(OpenHomeBuilder):
    def setup(self):
        self.nuget_server = self.env.get('NUGET_SERVER', None)
        self.nuget_api_key = self.env.get('NUGET_API_KEY', None)
        self.set_nuget_sln('src/ohGit.sln')
        self.packagepath = os.path.join(os.getcwd(),'build', 'packages');

        if not os.path.exists(self.packagepath):
            os.makedirs(self.packagepath)

    def configure(self):
        self.set_nunit_location(glob.glob('dependencies/nuget/NUnit.Runners*/tools/nunit-console-x86.exe')[0])
        self.set_cover_location(glob.glob('dependencies/nuget/OpenCover*/OpenCover.Console.exe')[0])

    def clean(self):
        self.msbuild('src/ohGit.sln', target='Clean', configuration=self.configuration)

    def build(self):
        self.msbuild('src/ohGit.sln', target='Build', configuration=self.configuration)
        self.pack_nuget('src/ohGit/ohGit.nuspec', 'build/ohGit/bin/{0}'.format(self.configuration))

    def publish(self):
        if self.options.auto and not self.platform == 'Windows-x86':
            print "Publish on %s platform is not enabled due to auto" % (self.platform)
            # Only publish from one CI platform: Windows-x86.
            return
        # build the nuget package
        if self.configuration == 'Release' and self.nuget_server is not None and self.nuget_api_key is not None:
            print "Publishing nuget on %s platform is enabled" % (self.platform)
            self.publish_nuget(os.path.join('build', 'packages', '*.nupkg'), self.nuget_api_key, self.nuget_server)
        else:
            print("Not publishing nuget dependency, nuget server and API key not specified")
        
