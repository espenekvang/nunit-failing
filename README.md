# nunit-failing
Simple project illustrating a situation where NUnit tests run in VS2017, but when trying to run them using nunit-console runner they fail

## To reproduce
1. Open solution in VS2017
2. Run all tests in testproject Nunit.Failing.Test.Unit
3. Watch both tests pass
4. Open a powershell instance and navigate to project root
5. run _build.cmd_
6. Watch it failing with 1 of 2 tests
