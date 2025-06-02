# HackerOS Pipeline Integration Test Script

## Test Setup
This script contains a series of pipeline tests to verify that the advanced shell features are working correctly.

## Basic Pipeline Tests

### Test 1: Simple Pipe (cat | grep)
```bash
cat pipeline-test.txt | grep test
```
Expected: Should show lines containing "test"

### Test 2: Multi-stage Pipeline (cat | grep | head)  
```bash
echo -e "line1\nline2\ntest1\nline3\ntest2\nline4" | grep test | head -n 1
```
Expected: Should show "test1"

### Test 3: Basic Output Redirection
```bash
echo "Hello World" > output-test.txt
cat output-test.txt
```
Expected: Should create file and display "Hello World"

### Test 4: Input Redirection
```bash
echo "test input" > input-test.txt
cat < input-test.txt
```
Expected: Should display "test input"

### Test 5: Append Redirection
```bash
echo "Line 1" > append-test.txt
echo "Line 2" >> append-test.txt
cat append-test.txt
```
Expected: Should show both lines

## Advanced Operator Tests

### Test 6: AND Operator (&&)
```bash
echo "first" && echo "second"
```
Expected: Should show both "first" and "second"

### Test 7: OR Operator (||)
```bash
false || echo "fallback executed"
```
Expected: Should show "fallback executed"

### Test 8: Sequential Operator (;)
```bash
echo "command1"; echo "command2"
```
Expected: Should show both commands regardless of exit codes

### Test 9: Complex Pipeline with Operators
```bash
echo "test data" | grep "test" && echo "found test" || echo "not found"
```
Expected: Should show "test data" and "found test"

## Error Handling Tests

### Test 10: Command Not Found
```bash
nonexistentcommand | echo "after pipe"
```
Expected: Should show error for first command, may or may not execute second

### Test 11: File Not Found
```bash
cat nonexistent.txt | grep test
```
Expected: Should show file not found error

### Test 12: Pipeline with Failing Command
```bash
echo "data" | false | echo "after false"
```
Expected: Behavior depends on pipeline error handling

## Performance Tests

### Test 13: Large Data Pipeline
```bash
echo -e "$(for i in {1..100}; do echo "line$i test data"; done)" | grep test | head -n 5
```
Expected: Should handle large data efficiently

### Test 14: Multiple Concurrent Streams
```bash
(echo "stream1" | cat) & (echo "stream2" | cat) & wait
```
Expected: Should handle concurrent execution

## Environment Variable Tests

### Test 15: Variable Expansion in Pipeline
```bash
export TEST_VAR="hello"
echo "$TEST_VAR world" | cat
```
Expected: Should show "hello world"

## Cleanup Tests

### Test 16: Resource Cleanup Verification
```bash
# This test should be run while monitoring for memory leaks
for i in {1..10}; do echo "test$i" | cat > /dev/null; done
```
Expected: Should not leak memory or file handles

## Instructions for Manual Testing

1. Run each test individually in the HackerOS shell
2. Verify the expected output matches actual output  
3. Check for any error messages or exceptions
4. Monitor system resources during performance tests
5. Verify all temporary files are cleaned up properly

## Expected Results Summary

If all tests pass:
- Basic piping works correctly
- All redirection operators function properly  
- Boolean operators (&&, ||, ;) work as expected
- Error handling is appropriate
- Performance is acceptable for normal use cases
- Resources are properly cleaned up

## Troubleshooting Common Issues

1. **Commands not found**: Verify command registration in CommandInitializer
2. **Pipe not working**: Check StreamManager integration in Shell.cs
3. **Redirection failing**: Verify RedirectionManager implementation
4. **Memory leaks**: Check stream disposal in pipeline execution
5. **Performance issues**: Profile stream handling and async operations
