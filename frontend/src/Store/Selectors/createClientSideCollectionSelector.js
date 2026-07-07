import _ from 'lodash';
import { createSelector } from 'reselect';
import { filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { FILTER_COMBINATOR_OR, isFilterGroup, normalizeFilterGroup } from 'Utilities/Filter/filterTree';
import findSelectedFilters from 'Utilities/Filter/findSelectedFilters';

function getSortClause(sortKey, sortDirection, sortPredicates) {
  if (sortPredicates && sortPredicates.hasOwnProperty(sortKey)) {
    return function(item) {
      return sortPredicates[sortKey](item, sortDirection);
    };
  }

  return function(item) {
    return item[sortKey];
  };
}

function doesFilterMatch(item, filter, filterPredicates) {
  const {
    key,
    value,
    type = filterTypes.EQUAL
  } = filter;

  if (filterPredicates && filterPredicates.hasOwnProperty(key)) {
    const predicate = filterPredicates[key];

    if (Array.isArray(value)) {
      if (
        type === filterTypes.NOT_CONTAINS ||
        type === filterTypes.NOT_EQUAL
      ) {
        return value.every((v) => predicate(item, v, type));
      }

      return value.some((v) => predicate(item, v, type));
    }

    return predicate(item, value, type);
  } else if (item.hasOwnProperty(key)) {
    const predicate = filterTypePredicates[type];

    if (Array.isArray(value)) {
      if (
        type === filterTypes.NOT_CONTAINS ||
        type === filterTypes.NOT_EQUAL
      ) {
        return value.every((v) => predicate(item[key], v));
      }

      return value.some((v) => predicate(item[key], v));
    }

    return predicate(item[key], value);
  }

  // Default to false if the filter can't be tested.
  return false;
}

function doesFilterGroupMatch(item, group, filterPredicates) {
  const filters = group.filters || [];

  if (!filters.length) {
    return true;
  }

  if (group.combinator === FILTER_COMBINATOR_OR) {
    return filters.some((childFilter) => {
      return isFilterGroup(childFilter) ?
        doesFilterGroupMatch(item, childFilter, filterPredicates) :
        doesFilterMatch(item, childFilter, filterPredicates);
    });
  }

  return filters.every((childFilter) => {
    return isFilterGroup(childFilter) ?
      doesFilterGroupMatch(item, childFilter, filterPredicates) :
      doesFilterMatch(item, childFilter, filterPredicates);
  });
}

function filterItems(items, state) {
  const {
    selectedFilterKey,
    filters,
    customFilters,
    filterPredicates
  } = state;

  if (!selectedFilterKey) {
    return items;
  }

  const selectedFilters = findSelectedFilters(selectedFilterKey, filters, customFilters);
  const selectedFilterGroup = normalizeFilterGroup(selectedFilters);

  return _.filter(items, (item) => {
    return doesFilterGroupMatch(item, selectedFilterGroup, filterPredicates);
  });
}

function sort(items, state) {
  const {
    sortKey,
    sortDirection,
    sortPredicates,
    secondarySortKey,
    secondarySortDirection
  } = state;

  const clauses = [];
  const orders = [];

  clauses.push(getSortClause(sortKey, sortDirection, sortPredicates));
  orders.push(sortDirection === sortDirections.ASCENDING ? 'asc' : 'desc');

  if (secondarySortKey &&
      secondarySortDirection &&
      (sortKey !== secondarySortKey ||
       sortDirection !== secondarySortDirection)) {
    clauses.push(getSortClause(secondarySortKey, secondarySortDirection, sortPredicates));
    orders.push(secondarySortDirection === sortDirections.ASCENDING ? 'asc' : 'desc');
  }

  return _.orderBy(items, clauses, orders);
}

export function createCustomFiltersSelector(type, alternateType) {
  return createSelector(
    (state) => state.customFilters.items,
    (customFilters) => {
      return customFilters.filter((customFilter) => {
        return customFilter.type === type || customFilter.type === alternateType;
      });
    }
  );
}

function createClientSideCollectionSelector(section, uiSection) {
  return createSelector(
    (state) => _.get(state, section),
    (state) => _.get(state, uiSection),
    createCustomFiltersSelector(section, uiSection),
    (sectionState, uiSectionState = {}, customFilters) => {
      const state = Object.assign({}, sectionState, uiSectionState, { customFilters });

      const filtered = filterItems(state.items, state);
      const sorted = sort(filtered, state);

      return {
        ...sectionState,
        ...uiSectionState,
        customFilters,
        items: sorted,
        totalItems: state.items.length
      };
    }
  );
}

export default createClientSideCollectionSelector;
