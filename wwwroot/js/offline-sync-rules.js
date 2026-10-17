/*
 * Offline sync rules, shared by the service worker (sw.js, importScripts) and the unit tests (node --test).
 * Pure functions only : no network, no storage.
 *
 * Status of a change saved offline (outbox item) :
 *   pending        waiting to be sent
 *   syncing        being sent right now
 *   retry          temporary problem (no internet, server error) : sent again automatically
 *   requires_auth  the login has ended : sent again automatically after the next login
 *   failed         refused by the server (permission / validation) : kept on the device, only sent again by "Retry"
 *   conflict       the server could not confirm whether it was saved : kept, the user checks the list first
 * A change confirmed by the server is removed from the device (it is then a normal server record).
 */
(function (root) {
	'use strict';

	const MAX_SERVER_ATTEMPTS = 5;

	const STATUS_LABELS = {
		pending: 'Waiting to sync',
		syncing: 'Syncing',
		retry: 'Retry required',
		requires_auth: 'Login required',
		conflict: 'Conflict',
		failed: 'Failed'
	};

	const CATEGORY_LABELS = {
		permission: 'Permission denied',
		validation: 'Validation failed',
		server: 'Server error'
	};

	// fields shown as the name of the record in the "offline changes" list (first one found)
	const TITLE_FIELDS = ['Name', 'Obj.Name', 'Fullname', 'FirstName', 'Obj.FirstName', 'Title', 'Obj.UserName', 'UserName', 'Notes', 'Remark', 'Type'];
	const ID_FIELDS = ['Id', 'Obj.Id'];

	const PERMISSION_MESSAGE = /do not have access|not allowed|permission|another user|only a vendor|not subscribe|subscription|plan not active|plan is cancelled/i;

	/* What a queued request is, for the user : module (controller), action, record name */
	function describeChange(url, fields) {
		let path = '';
		try { path = new URL(url, 'http://local').pathname; } catch (e) { path = String(url || ''); }

		const parts = path.split('/').filter(Boolean);           // Admin / Project / Save
		const module = (parts.length >= 2 ? parts[parts.length - 2] : parts[0] || '').toLowerCase();
		const actionName = (parts[parts.length - 1] || '').toLowerCase();

		const get = (names) => {
			for (const name of names) {
				const value = fields && fields[name];
				if (value !== undefined && value !== null && String(value).trim() !== '') return String(value).trim();
			}
			return '';
		};

		const recordId = Number(get(ID_FIELDS) || (() => { try { return new URL(url, 'http://local').searchParams.get('Id'); } catch (e) { return ''; } })() || 0) || 0;

		let action = 'update';
		if (/delete/.test(actionName)) action = 'delete';
		else if (/status/.test(actionName)) action = 'status';
		else if (recordId <= 0) action = 'create';

		const title = get(TITLE_FIELDS).slice(0, 80) || (recordId > 0 ? '#' + recordId : '');

		return { module: module, action: action, title: title, recordId: recordId };
	}

	/*
	 * Result of sending a queued change again.
	 * input : { networkError, redirectedToLogin, httpStatus, syncHeader, json }
	 * output : { status, category, message, stop } - stop = do not send the next changes now (keep their order)
	 */
	function classifyReplay(input) {
		const r = input || {};

		if (r.networkError)
			return { status: 'retry', category: 'network', message: 'No connection to the server.', stop: true };

		if (r.redirectedToLogin)
			return { status: 'requires_auth', category: 'auth', message: 'Your login has ended. Please login again to send this change.', stop: true };

		const json = r.json && typeof r.json === 'object' ? r.json : null;
		const message = json && json.Message ? String(json.Message) : '';

		if (r.httpStatus === 409 && r.syncHeader === 'unknown')
			return { status: 'conflict', category: 'conflict', message: message || 'The server could not confirm whether this change was saved.', stop: false };

		if (r.httpStatus === 409)
			return { status: 'retry', category: 'server', message: message || 'The change is still being saved.', stop: true };

		if (r.httpStatus === 401)
			return { status: 'requires_auth', category: 'auth', message: 'Your login has ended. Please login again to send this change.', stop: true };

		if (r.httpStatus === 403)
			return { status: 'failed', category: 'permission', message: message || 'You do not have access to perform this action.', stop: false };

		if (r.httpStatus >= 500 || r.httpStatus === 0 || r.httpStatus === 408 || r.httpStatus === 429)
			return { status: 'retry', category: 'server', message: 'The server is not available right now (' + (r.httpStatus || 'no answer') + ').', stop: true };

		if (json && typeof json.StatusCode !== 'undefined') {
			if (json.StatusCode === 1 || json.StatusCode === 10)
				return { status: 'synced', category: null, message: message, stop: false };

			return { status: 'failed', category: PERMISSION_MESSAGE.test(message) ? 'permission' : 'validation', message: message || 'The server did not accept this change.', stop: false };
		}

		if (r.httpStatus >= 400)
			return { status: 'failed', category: 'validation', message: 'The server did not accept this change (' + r.httpStatus + ').', stop: false };

		// 2xx without the usual answer (e.g. an error page) : try again later
		return { status: 'retry', category: 'server', message: 'Unexpected answer from the server.', stop: true };
	}

	/* After a failed attempt : too many server errors -> stop retrying automatically */
	function nextStatus(outcome, attempts) {
		if (outcome.status === 'retry' && outcome.category === 'server' && attempts >= MAX_SERVER_ATTEMPTS) return 'failed';
		return outcome.status;
	}

	/* Sent by the automatic sync ? (failed / conflict only when the user asks for "Retry") */
	function isAutoSyncable(item) {
		return !!item && ['pending', 'syncing', 'retry', 'requires_auth'].indexOf(item.status || 'pending') > -1;
	}

	function needsAttention(item) {
		return !!item && (item.status === 'failed' || item.status === 'conflict');
	}

	function statusLabel(item) {
		if (!item) return '';
		if (item.status === 'failed' && CATEGORY_LABELS[item.category]) return CATEGORY_LABELS[item.category];
		return STATUS_LABELS[item.status || 'pending'] || item.status;
	}

	const api = { MAX_SERVER_ATTEMPTS, describeChange, classifyReplay, nextStatus, isAutoSyncable, needsAttention, statusLabel };

	if (typeof module === 'object' && module.exports) module.exports = api;
	else root.LeozSyncRules = api;
})(typeof self !== 'undefined' ? self : this);
