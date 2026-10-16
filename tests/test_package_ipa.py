# SPDX-License-Identifier: Apache-2.0
import importlib.util
import plistlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("package_ipa", Path(__file__).parents[1] / "tools/package_ipa.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PackagePlatformTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.app = Path(self.temporary.name) / "WA2.app"
        self.app.mkdir()
        (self.app / "WA2").write_bytes(b"fixture")
        self.info = {"CFBundleExecutable": "WA2", "UIDeviceFamily": [3],
                     "UIApplicationSceneManifest": {"UISceneConfigurations": {"UIWindowSceneSessionRoleApplication": [{}]}}}

    def write_info(self):
        (self.app / "Info.plist").write_bytes(plistlib.dumps(self.info))

    def commands(self, command, **kwargs):
        if command[0] == "lipo": return "arm64\n"
        return "platform TVOS\nminos 17.0\n"

    def test_tvos_device_bundle_is_accepted(self):
        self.write_info()
        with patch.object(module.subprocess, "check_output", side_effect=self.commands):
            self.assertEqual([3], module.validate(self.app, "tvos")["UIDeviceFamily"])

    def test_ios_bundle_cannot_be_relabelled_tvos(self):
        self.info["UIDeviceFamily"] = [1, 2]
        self.write_info()
        with self.assertRaisesRegex(ValueError, "Apple TV only"):
            module.validate(self.app, "tvos")

    def test_ios_executable_cannot_be_packaged_for_tvos(self):
        self.write_info()
        with patch.object(module.subprocess, "check_output", side_effect=["arm64\n", "platform IOS\n"]):
            with self.assertRaisesRegex(ValueError, "requested device platform"):
                module.validate(self.app, "tvos")

    def test_simulator_executable_is_rejected(self):
        self.write_info()
        with patch.object(module.subprocess, "check_output", side_effect=["arm64\n", "platform TVOSSIMULATOR\n"]):
            with self.assertRaisesRegex(ValueError, "requested device platform"):
                module.validate(self.app, "tvos")

    def test_foreign_embedded_framework_is_rejected(self):
        self.write_info()
        framework = self.app / "Frameworks/wa2.framework"
        framework.mkdir(parents=True)
        (framework / "wa2").write_bytes(b"fixture")
        with patch.object(module.subprocess, "check_output", side_effect=["arm64\n", "platform TVOS\n", "platform IOS\n"]):
            with self.assertRaisesRegex(ValueError, "wrong platform"):
                module.validate(self.app, "tvos")

    def test_existing_ios_validation_remains_compatible(self):
        self.info.update(UIDeviceFamily=[1, 2], UIFileSharingEnabled=True, LSSupportsOpeningDocumentsInPlace=True)
        self.write_info()
        with patch.object(module.subprocess, "check_output", side_effect=["arm64\n", "platform IOS\n"]):
            self.assertEqual([1, 2], module.validate(self.app)["UIDeviceFamily"])
