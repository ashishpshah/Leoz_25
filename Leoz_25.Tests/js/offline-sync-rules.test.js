// Offline sync rules (wwwroot/js/offline-sync-rules.js). Run : node --test Leoz_25.Tests/js
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const Rules = require(path.join(__dirname, '..', '..', 'wwwroot', 'js', 'offline-sync-rules.js'));

test('describeChange : new Project from the Project form', () => {
	const d = Rules.describeChange('https://leozpmc.com/Admin/Project/Save', { Id: '0', Name: 'Site A', StartDate_Text: '2026-10-17' });
	assert.deepEqual(d, { module: 'project', action: 'create', title: 'Site A', recordId: 0 });
});

test('describeChange : edit, delete and status changes', () => {
	assert.equal(Rules.describeChange('/Admin/Project/Save', { Id: '15', Name: 'Site B' }).action, 'update');
	assert.equal(Rules.describeChange('/Admin/Customer/DeleteConfirmed?Id=9', {}).action, 'delete');
	assert.equal(Rules.describeChange('/Admin/Customer/DeleteConfirmed?Id=9', {}).recordId, 9);
	assert.equal(Rules.describeChange('/Admin/Project/Save_Doc_Status?Id=3&Status=A', {}).action, 'status');
	assert.equal(Rules.describeChange('/Admin/User/Save', { 'Obj.Id': '4', 'Obj.UserName': 'ravi' }).title, 'ravi');
});

test('classifyReplay : confirmed save is synced', () => {
	const r = Rules.classifyReplay({ httpStatus: 200, json: { StatusCode: 1, Message: 'Record saved successfully !' } });
	assert.equal(r.status, 'synced');
});

test('classifyReplay : the original error (Admin saving a Project) is a permission failure, kept, not retried automatically', () => {
	const r = Rules.classifyReplay({ httpStatus: 200, json: { StatusCode: 0, Message: 'You do not have access to perform this action.' } });
	assert.equal(r.status, 'failed');
	assert.equal(r.category, 'permission');
	assert.equal(r.stop, false);
	assert.equal(Rules.isAutoSyncable({ status: r.status }), false);
	assert.equal(Rules.needsAttention({ status: r.status }), true);
});

test('classifyReplay : business validation is a validation failure', () => {
	const r = Rules.classifyReplay({ httpStatus: 200, json: { StatusCode: 0, Message: 'Project already exist. Please try another Project name(s).' } });
	assert.equal(r.status, 'failed');
	assert.equal(r.category, 'validation');
});

test('classifyReplay : subscription problem counts as permission', () => {
	const r = Rules.classifyReplay({ httpStatus: 200, json: { StatusCode: 0, Message: 'You are not subscribe any plan. Please subscribe a plan first.' } });
	assert.equal(r.category, 'permission');
});

test('classifyReplay : no internet / server down = retry and keep the order', () => {
	for (const input of [{ networkError: true }, { httpStatus: 500 }, { httpStatus: 503 }, { httpStatus: 0 }]) {
		const r = Rules.classifyReplay(input);
		assert.equal(r.status, 'retry', JSON.stringify(input));
		assert.equal(r.stop, true);
	}
});

test('classifyReplay : login ended = requires_auth (sent after the next login)', () => {
	const r = Rules.classifyReplay({ redirectedToLogin: true, httpStatus: 200 });
	assert.equal(r.status, 'requires_auth');
	assert.equal(r.stop, true);
	assert.equal(Rules.isAutoSyncable({ status: 'requires_auth' }), true);
	assert.equal(Rules.classifyReplay({ httpStatus: 401 }).status, 'requires_auth');
});

test('classifyReplay : duplicate check answers', () => {
	assert.equal(Rules.classifyReplay({ httpStatus: 409, syncHeader: 'in-progress', json: { StatusCode: 0 } }).status, 'retry');
	assert.equal(Rules.classifyReplay({ httpStatus: 409, syncHeader: 'unknown', json: { StatusCode: 0 } }).status, 'conflict');
	assert.equal(Rules.classifyReplay({ httpStatus: 403, syncHeader: 'other-user', json: { StatusCode: 0, Message: 'x' } }).category, 'permission');
	// a replayed (already saved) answer is a normal success
	assert.equal(Rules.classifyReplay({ httpStatus: 200, syncHeader: 'replayed', json: { StatusCode: 1 } }).status, 'synced');
});

test('classifyReplay : unexpected HTML answer is retried, not reported as saved', () => {
	const r = Rules.classifyReplay({ httpStatus: 200, json: null });
	assert.equal(r.status, 'retry');
});

test('nextStatus : repeated server errors stop automatic retries', () => {
	const outcome = { status: 'retry', category: 'server' };
	assert.equal(Rules.nextStatus(outcome, 1), 'retry');
	assert.equal(Rules.nextStatus(outcome, Rules.MAX_SERVER_ATTEMPTS), 'failed');
	assert.equal(Rules.nextStatus({ status: 'retry', category: 'network' }, 99), 'retry'); // no internet never gives up
});

test('isAutoSyncable : statuses sent by the automatic sync', () => {
	for (const s of ['pending', 'syncing', 'retry', 'requires_auth']) assert.equal(Rules.isAutoSyncable({ status: s }), true, s);
	for (const s of ['failed', 'conflict']) assert.equal(Rules.isAutoSyncable({ status: s }), false, s);
	assert.equal(Rules.isAutoSyncable({}), true); // items queued by older versions have no status
});

test('statusLabel : user facing names', () => {
	assert.equal(Rules.statusLabel({ status: 'pending' }), 'Waiting to sync');
	assert.equal(Rules.statusLabel({ status: 'failed', category: 'permission' }), 'Permission denied');
	assert.equal(Rules.statusLabel({ status: 'failed', category: 'validation' }), 'Validation failed');
	assert.equal(Rules.statusLabel({ status: 'requires_auth' }), 'Login required');
});
