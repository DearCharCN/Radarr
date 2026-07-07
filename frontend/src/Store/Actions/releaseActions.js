import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import sortByProp from 'Utilities/Array/sortByProp';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import translate from 'Utilities/String/translate';
import { set, update } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';

//
// Variables

export const section = 'releases';

let abortCurrentRequest = null;
let mediaInfoAbortRequests = [];
let mediaInfoSearchId = 0;

const mediaInfoConcurrency = 4;
const mediaInfoSortKeys = ['audioInfo', 'subs'];

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  isMediaInfoFetching: false,
  isMediaInfoComplete: false,
  mediaInfoTotal: 0,
  mediaInfoCompleted: 0,
  error: null,
  items: [],
  sortKey: 'releaseWeight',
  sortDirection: sortDirections.ASCENDING,
  sortPredicates: {
    age: function(item, direction) {
      return item.ageMinutes;
    },

    peers: function(item, direction) {
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;

      return seeders * 1000000 + leechers;
    },

    languages: function(item, direction) {
      if (item.languages.length > 1) {
        return 10000;
      }

      return item.languages[0]?.id ?? 0;
    },

    audioInfo: function(item, direction) {
      return getMediaInfoSortValue(item, 'audioInfo', direction);
    },

    subs: function(item, direction) {
      return getMediaInfoSortValue(item, 'subs', direction);
    },

    indexerFlags: function(item, direction) {
      const indexerFlags = item.indexerFlags;
      const releaseWeight = item.releaseWeight;

      if (indexerFlags.length === 0) {
        return releaseWeight + 1000000;
      }

      return releaseWeight;
    },

    rejections: function(item, direction) {
      const rejections = item.rejections;
      const releaseWeight = item.releaseWeight;

      if (rejections.length !== 0) {
        return releaseWeight + 1000000;
      }

      return releaseWeight;
    }
  },

  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: []
    }
  ],

  filterPredicates: {
    quality: function(item, value, type) {
      const qualityId = item.quality.quality.id;

      if (type === filterTypes.EQUAL) {
        return qualityId === value;
      }

      if (type === filterTypes.NOT_EQUAL) {
        return qualityId !== value;
      }

      // Default to false
      return false;
    },

    languages: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      const languages = item.languages.map((language) => language.name);

      return predicate(languages, filterValue);
    },

    audioInfo: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(getAudioSummary(item), filterValue);
    },

    subs: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(getListSummary(item.subs), filterValue);
    },

    mediaInfoStatus: function(item, filterValue, type) {
      const predicate = filterTypePredicates[type];

      return predicate(getMediaInfoStatusSummary(item), filterValue);
    },

    peers: function(item, value, type) {
      const predicate = filterTypePredicates[type];
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;

      return predicate(seeders + leechers, value);
    },

    rejectionCount: function(item, value, type) {
      const rejectionCount = item.rejections.length;

      switch (type) {
        case filterTypes.EQUAL:
          return rejectionCount === value;

        case filterTypes.GREATER_THAN:
          return rejectionCount > value;

        case filterTypes.GREATER_THAN_OR_EQUAL:
          return rejectionCount >= value;

        case filterTypes.LESS_THAN:
          return rejectionCount < value;

        case filterTypes.LESS_THAN_OR_EQUAL:
          return rejectionCount <= value;

        case filterTypes.NOT_EQUAL:
          return rejectionCount !== value;

        default:
          return false;
      }
    }
  },

  filterBuilderProps: [
    {
      name: 'title',
      label: () => translate('Title'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'age',
      label: () => translate('Age'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'protocol',
      label: () => translate('Protocol'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.PROTOCOL
    },
    {
      name: 'indexerId',
      label: () => translate('Indexer'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.INDEXER
    },
    {
      name: 'size',
      label: () => translate('Size'),
      type: filterBuilderTypes.NUMBER,
      valueType: filterBuilderValueTypes.BYTES
    },
    {
      name: 'seeders',
      label: () => translate('Seeders'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'peers',
      label: () => translate('Peers'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'quality',
      label: () => translate('Quality'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY
    },
    {
      name: 'languages',
      label: () => translate('Languages'),
      type: filterBuilderTypes.ARRAY,
      optionsSelector: function(items) {
        const genreList = items.reduce((acc, release) => {
          release.languages.forEach((language) => {
            acc.push({
              id: language.name,
              name: language.name
            });
          });

          return acc;
        }, []);

        return genreList.sort(sortByProp('name'));
      }
    },
    {
      name: 'audioInfo',
      label: () => translate('AudioInfo'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'subs',
      label: () => translate('SubtitleLanguages'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'mediaInfoStatus',
      label: () => translate('AdditionalData'),
      type: filterBuilderTypes.STRING
    },
    {
      name: 'customFormatScore',
      label: () => translate('CustomFormatScore'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'rejectionCount',
      label: () => translate('RejectionCount'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'movieRequested',
      label: () => translate('MovieRequested'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    }
  ],
  selectedFilterKey: 'all'

};

export const persistState = [
  'releases.customFilters',
  'releases.selectedFilterKey'
];

//
// Actions Types

export const FETCH_RELEASES = 'releases/fetchReleases';
export const CANCEL_FETCH_RELEASES = 'releases/cancelFetchReleases';
export const SET_RELEASES_SORT = 'releases/setReleasesSort';
export const CLEAR_RELEASES = 'releases/clearReleases';
export const GRAB_RELEASE = 'releases/grabRelease';
export const UPDATE_RELEASE = 'releases/updateRelease';
export const SET_RELEASES_FILTER = 'releases/setMovieReleasesFilter';

//
// Action Creators

export const fetchReleases = createThunk(FETCH_RELEASES);
export const cancelFetchReleases = createThunk(CANCEL_FETCH_RELEASES);
export const setReleasesSort = createAction(SET_RELEASES_SORT);
export const clearReleases = createAction(CLEAR_RELEASES);
export const grabRelease = createThunk(GRAB_RELEASE);
export const updateRelease = createAction(UPDATE_RELEASE);
export const setReleasesFilter = createAction(SET_RELEASES_FILTER);

//
// Helpers

function formatAudioInfo(audioInfo) {
  const language = audioInfo.language?.trim();
  const specification = audioInfo.specification?.trim();

  if (language && specification) {
    return `${language}: ${specification}`;
  }

  return language || specification || '';
}

function getListSummary(values = []) {
  return values.filter(Boolean).join(', ');
}

function getAudioSummary(item) {
  const {
    audioInfo = []
  } = item;

  return audioInfo.map(formatAudioInfo).filter(Boolean).join('; ');
}

function getMediaInfoStatusSummary(item) {
  if (item.mediaInfoStatus) {
    return item.mediaInfoStatus;
  }

  if (item.mediaInfoProgressStatus) {
    return item.mediaInfoProgressStatus;
  }

  return '';
}

function getMediaInfoSortSummary(item, sortKey) {
  if (sortKey === 'audioInfo') {
    return getAudioSummary(item);
  }

  if (sortKey === 'subs') {
    return getListSummary(item.subs);
  }

  if (sortKey === 'mediaInfoStatus') {
    return getMediaInfoStatusSummary(item);
  }

  return '';
}

function isMediaInfoSortKey(sortKey) {
  return mediaInfoSortKeys.includes(sortKey);
}

function getMediaInfoSortValue(item, sortKey, sortDirection) {
  const sortValue = item.mediaInfoSortValues?.[sortKey];
  const normalized = sortValue?.trim().toLocaleLowerCase();
  const hasValueGroup = sortDirection === sortDirections.DESCENDING ? '1' : '0';
  const emptyGroup = sortDirection === sortDirections.DESCENDING ? '0' : '1';

  if (!normalized) {
    return `${emptyGroup}|`;
  }

  return `${hasValueGroup}|${normalized}`;
}

function snapshotMediaInfoSortValues(items, sortKey) {
  return items.map((item) => {
    return {
      ...item,
      mediaInfoSortValues: {
        ...item.mediaInfoSortValues,
        [sortKey]: getMediaInfoSortSummary(item, sortKey)
      }
    };
  });
}

function getMediaInfoProgress(releases = []) {
  const progressRelease = releases.find((release) => {
    return release.mediaInfoProgressStatus;
  });

  if (progressRelease) {
    const total = progressRelease.mediaInfoProgressTotal || 0;
    const completed = progressRelease.mediaInfoProgressCompleted || 0;

    return {
      isMediaInfoFetching: progressRelease.mediaInfoProgressStatus === 'pending',
      isMediaInfoComplete: progressRelease.mediaInfoProgressStatus === 'completed',
      mediaInfoTotal: total,
      mediaInfoCompleted: completed
    };
  }

  const pendingCount = releases.filter((release) => {
    return release.mediaInfoStatus === 'pending';
  }).length;

  return {
    isMediaInfoFetching: pendingCount > 0,
    isMediaInfoComplete: pendingCount === 0,
    mediaInfoTotal: pendingCount,
    mediaInfoCompleted: 0
  };
}

function hasPendingMediaInfo(releases = []) {
  return releases.some((release) => {
    return release.mediaInfoStatus === 'pending' ||
      release.mediaInfoProgressStatus === 'pending';
  });
}

function mergeReleaseMediaInfo(releases, payload) {
  const index = releases.findIndex((release) => {
    return release.guid === payload.guid &&
      (payload.indexerId == null || release.indexerId === payload.indexerId);
  });

  if (index < 0) {
    return releases;
  }

  const updatedRelease = {
    ...releases[index],
    ...payload
  };
  const updatedReleases = [...releases];
  updatedReleases.splice(index, 1, updatedRelease);

  return updatedReleases;
}

function clearMediaInfoPolling() {
  mediaInfoSearchId++;

  mediaInfoAbortRequests.forEach((abortRequest) => abortRequest());
  mediaInfoAbortRequests = [];
}

function removeMediaInfoAbortRequest(abortRequest) {
  mediaInfoAbortRequests = mediaInfoAbortRequests.filter((request) => {
    return request !== abortRequest;
  });
}

function fetchReleaseMediaInfo(releases, dispatch, getState) {
  const pendingReleases = releases.filter((release) => {
    return release.mediaInfoStatus === 'pending';
  });

  dispatch(set({
    section,
    ...getMediaInfoProgress(releases)
  }));

  if (!pendingReleases.length) {
    return;
  }

  const searchId = mediaInfoSearchId;
  const queue = [...pendingReleases];
  let activeRequests = 0;

  function startNext() {
    if (searchId !== mediaInfoSearchId) {
      return;
    }

    while (activeRequests < mediaInfoConcurrency && queue.length) {
      const {
        guid,
        indexerId,
        prowlarrIndexerId,
        mediaInfoSearchId: releaseMediaInfoSearchId
      } = queue.shift();

      activeRequests++;

      const {
        request,
        abortRequest
      } = createAjaxRequest({
        url: '/release/mediaInfo',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ guid, indexerId, prowlarrIndexerId, mediaInfoSearchId: releaseMediaInfoSearchId })
      });

      mediaInfoAbortRequests.push(abortRequest);

      request.done((data) => {
        if (searchId === mediaInfoSearchId) {
          const releaseState = getSectionState(getState(), section);
          const updatedReleases = mergeReleaseMediaInfo(releaseState.items, data);

          dispatch(batchActions([
            update({ section, data: updatedReleases }),
            set({
              section,
              ...getMediaInfoProgress(updatedReleases)
            })
          ]));
        }
      });

      request.fail((xhr) => {
        if (searchId === mediaInfoSearchId && !xhr.aborted) {
          const releaseState = getSectionState(getState(), section);
          const updatedReleases = mergeReleaseMediaInfo(releaseState.items, {
            guid,
            indexerId,
            mediaInfoStatus: 'failed'
          });

          dispatch(batchActions([
            update({ section, data: updatedReleases }),
            set({
              section,
              ...getMediaInfoProgress(updatedReleases)
            })
          ]));
        }
      });

      request.always(() => {
        activeRequests--;
        removeMediaInfoAbortRequest(abortRequest);

        startNext();
      });
    }
  }

  startNext();
}

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_RELEASES]: function(getState, payload, dispatch) {
    clearMediaInfoPolling();

    dispatch(set({
      section,
      isFetching: true,
      isMediaInfoFetching: false,
      isMediaInfoComplete: false,
      mediaInfoTotal: 0,
      mediaInfoCompleted: 0
    }));

    const {
      id,
      ...otherPayload
    } = payload;

    const {
      request,
      abortRequest
    } = createAjaxRequest({
      url: id == null ? '/release' : `/release/${id}`,
      data: otherPayload,
      traditional: true
    });

    request.done((data) => {
      const releaseState = getSectionState(getState(), section);
      const releases = id == null && isMediaInfoSortKey(releaseState.sortKey) ?
        snapshotMediaInfoSortValues(data, releaseState.sortKey) :
        data;

      dispatch(batchActions([
        update({ section, data: releases }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
          ...getMediaInfoProgress(releases)
        })
      ]));

      if (id == null && hasPendingMediaInfo(releases)) {
        fetchReleaseMediaInfo(releases, dispatch, getState);
      }
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });

    abortCurrentRequest = function() {
      abortRequest();
      clearMediaInfoPolling();
    };
  },

  [CANCEL_FETCH_RELEASES]: function(getState, payload, dispatch) {
    if (abortCurrentRequest) {
      abortCurrentRequest = abortCurrentRequest();
    }
  },

  [GRAB_RELEASE]: function(getState, payload, dispatch) {
    const guid = payload.guid;

    dispatch(updateRelease({ guid, isGrabbing: true }));

    const promise = createAjaxRequest({
      url: '/release',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(payload)
    }).request;

    promise.done((data) => {
      dispatch(updateRelease({
        guid,
        isGrabbing: false,
        isGrabbed: true,
        grabError: null
      }));
    });

    promise.fail((xhr) => {
      const grabError = xhr.responseJSON && xhr.responseJSON.message || 'Failed to add to download queue';

      dispatch(updateRelease({
        guid,
        isGrabbing: false,
        isGrabbed: false,
        grabError
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_RELEASES]: (state) => {
    const {
      selectedFilterKey,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState);
  },

  [UPDATE_RELEASE]: (state, { payload }) => {
    const guid = payload.guid;
    const indexerId = payload.indexerId;
    const newState = Object.assign({}, state);
    const items = newState.items;
    const index = items.findIndex((item) => {
      return item.guid === guid && (indexerId == null || item.indexerId === indexerId);
    });

    // Don't try to update if there isn't a matching item (the user closed the modal)
    if (index >= 0) {
      const item = Object.assign({}, items[index], payload);

      newState.items = [...items];
      newState.items.splice(index, 1, item);
    }

    return newState;
  },

  [SET_RELEASES_FILTER]: createSetClientSideCollectionFilterReducer(section),
  [SET_RELEASES_SORT]: (state, { payload }) => {
    const newState = getSectionState(state, section);

    const sortKey = payload.sortKey || newState.sortKey;
    let sortDirection = payload.sortDirection;

    if (!sortDirection) {
      if (payload.sortKey === newState.sortKey) {
        sortDirection = newState.sortDirection === sortDirections.ASCENDING ?
          sortDirections.DESCENDING :
          sortDirections.ASCENDING;
      } else {
        sortDirection = newState.sortDirection;
      }
    }

    newState.sortKey = sortKey;
    newState.sortDirection = sortDirection;

    if (isMediaInfoSortKey(sortKey)) {
      newState.items = snapshotMediaInfoSortValues(newState.items, sortKey);
    }

    return updateSectionState(state, section, newState);
  }

}, defaultState, section);
