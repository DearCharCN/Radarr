import PropTypes from 'prop-types';
import React from 'react';
import SelectInput from 'Components/Form/SelectInput';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import { FILTER_COMBINATOR_AND, FILTER_COMBINATOR_OR, isFilterGroup } from 'Utilities/Filter/filterTree';
import FilterBuilderRow from './FilterBuilderRow';
import styles from './FilterBuilderModalContent.css';

const COMBINATOR_OPTIONS = [
  {
    key: FILTER_COMBINATOR_AND,
    value: 'and'
  },
  {
    key: FILTER_COMBINATOR_OR,
    value: 'or'
  }
];

function FilterBuilderGroup(props) {
  const {
    group,
    path,
    isRoot,
    filterCount,
    sectionItems,
    filterBuilderProps,
    onGroupChange,
    onFilterChange,
    onAddFilterPress,
    onAddGroupPress,
    onRemovePress
  } = props;

  const onCombinatorChange = ({ value }) => {
    onGroupChange(path, {
      ...group,
      combinator: value
    });
  };

  const onAddFilter = () => {
    onAddFilterPress(path);
  };

  const onAddGroup = () => {
    onAddGroupPress(path);
  };

  const onRemoveGroup = () => {
    onRemovePress(path);
  };

  return (
    <div className={isRoot ? styles.rootFilterGroup : styles.filterGroup}>
      <div className={styles.filterGroupHeader}>
        <div className={styles.combinatorContainer}>
          <SelectInput
            name="combinator"
            value={group.combinator || FILTER_COMBINATOR_AND}
            values={COMBINATOR_OPTIONS}
            onChange={onCombinatorChange}
          />
        </div>

        <div className={styles.groupActionsContainer}>
          <IconButton
            title="Add condition"
            name={icons.ADD}
            onPress={onAddFilter}
          />

          <IconButton
            title="Add group"
            name={icons.GROUP}
            onPress={onAddGroup}
          />

          {
            !isRoot &&
              <IconButton
                title="Remove group"
                name={icons.SUBTRACT}
                isDisabled={filterCount === 1}
                onPress={onRemoveGroup}
              />
          }
        </div>
      </div>

      <div className={styles.filterGroupChildren}>
        {
          group.filters.map((filter, index) => {
            const childPath = path.concat(index);

            if (isFilterGroup(filter)) {
              return (
                <FilterBuilderGroup
                  key={`group-${childPath.join('.')}`}
                  group={filter}
                  path={childPath}
                  isRoot={false}
                  filterCount={group.filters.length}
                  sectionItems={sectionItems}
                  filterBuilderProps={filterBuilderProps}
                  onGroupChange={onGroupChange}
                  onFilterChange={onFilterChange}
                  onAddFilterPress={onAddFilterPress}
                  onAddGroupPress={onAddGroupPress}
                  onRemovePress={onRemovePress}
                />
              );
            }

            return (
              <FilterBuilderRow
                key={`filter-${childPath.join('.')}-${filter.key}`}
                index={childPath}
                sectionItems={sectionItems}
                filterBuilderProps={filterBuilderProps}
                filterKey={filter.key}
                filterValue={filter.value}
                filterType={filter.type}
                filterCount={group.filters.length}
                onAddPress={onAddFilterPress}
                onRemovePress={onRemovePress}
                onFilterChange={onFilterChange}
              />
            );
          })
        }
      </div>
    </div>
  );
}

FilterBuilderGroup.propTypes = {
  group: PropTypes.object.isRequired,
  path: PropTypes.arrayOf(PropTypes.number).isRequired,
  isRoot: PropTypes.bool.isRequired,
  filterCount: PropTypes.number.isRequired,
  sectionItems: PropTypes.arrayOf(PropTypes.object).isRequired,
  filterBuilderProps: PropTypes.arrayOf(PropTypes.object).isRequired,
  onGroupChange: PropTypes.func.isRequired,
  onFilterChange: PropTypes.func.isRequired,
  onAddFilterPress: PropTypes.func.isRequired,
  onAddGroupPress: PropTypes.func.isRequired,
  onRemovePress: PropTypes.func.isRequired
};

export default FilterBuilderGroup;
