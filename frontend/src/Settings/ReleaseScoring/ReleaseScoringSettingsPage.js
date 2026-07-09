/* eslint-disable react/prop-types */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import SettingsToolbar from 'Settings/SettingsToolbar';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './ReleaseScoringSettingsPage.css';

const ENDPOINTS = {
  languageMapping: '/audiolanguagemapping',
  audioScoreProfile: '/audioscoreprofile',
  languagePreference: '/audiolanguagepreference'
};

const MATCH_TYPES = [
  { key: 'contains', value: 'Contains' },
  { key: 'normalizedContains', value: 'Normalized Contains' },
  { key: 'regex', value: 'Regex' }
];

const ORIGIN_LANGUAGE_TAG = 'Origin';

function requestJson({ url, method = 'GET', data }) {
  const ajaxOptions = {
    url,
    method
  };

  if (method !== 'DELETE') {
    ajaxOptions.dataType = 'json';
  }

  if (data !== undefined) {
    ajaxOptions.contentType = 'application/json';
    ajaxOptions.data = JSON.stringify(data);
  }

  return new Promise((resolve, reject) => {
    const { request } = createAjaxRequest(ajaxOptions);

    request.done((result) => resolve(result));
    request.fail((xhr) => reject(xhr));
  });
}

function getErrorMessage(xhr, fallback) {
  const response = xhr?.responseJSON;

  if (Array.isArray(response)) {
    return response
      .map((item) => item.errorMessage || item.message || item.propertyName)
      .filter(Boolean)
      .join(' ');
  }

  if (response?.errors) {
    return Object.values(response.errors).flat().join(' ');
  }

  return response?.message || response?.error || xhr?.statusText || fallback;
}

function getLanguageName(language) {
  return language?.name || translate('Unknown');
}

function getDefaultLanguage(languages) {
  return (
    languages.find((language) => language.name === 'Chinese') ||
    languages.find((language) => language.id > 0) ||
    languages[0] || { id: 0, name: 'Unknown', nameLower: 'unknown' }
  );
}

function createLanguageTagOptions(languages, entries = []) {
  const options = [];
  const seen = new Set();
  const excludedNames = new Set(['Any', 'Original', 'Unknown']);

  function addOption(tag) {
    const value = `${tag || ''}`.trim();

    if (!value) {
      return;
    }

    const key = value.toLowerCase();

    if (seen.has(key)) {
      return;
    }

    seen.add(key);
    options.push({
      key: value,
      value
    });
  }

  (languages || []).forEach((language) => {
    if (!language?.name || excludedNames.has(language.name)) {
      return;
    }

    addOption(language.name);
  });

  addOption(ORIGIN_LANGUAGE_TAG);

  entries.forEach((entry) => {
    addOption(entry.languageTag);
  });

  return options;
}

function getDefaultLanguageTag(languageTagOptions) {
  return languageTagOptions.find((option) => option.value === 'Chinese')?.value ||
    languageTagOptions[0]?.value ||
    ORIGIN_LANGUAGE_TAG;
}

function createDefaultLanguageMapping(languages) {
  const language = getDefaultLanguage(languages);
  const aliases = language.name === 'Chinese' ?
    ['Chinese', 'Mandarin', 'Guoyu'] :
    [language.name];

  return {
    id: 0,
    language,
    aliases,
    enabled: true
  };
}

function createDefaultAudioScoreProfile() {
  return {
    id: 0,
    name: translate('DefaultAudioScoreProfileName'),
    enabled: true,
    rules: [
      {
        name: 'Atmos',
        matchType: 'normalizedContains',
        pattern: 'Atmos',
        score: 50,
        mutexGroup: '',
        enabled: true
      },
      {
        name: '7.1',
        matchType: 'normalizedContains',
        pattern: '7.1',
        score: 30,
        mutexGroup: 'Channel Layout',
        enabled: true
      },
      {
        name: '5.1',
        matchType: 'normalizedContains',
        pattern: '5.1',
        score: 15,
        mutexGroup: 'Channel Layout',
        enabled: true
      },
      {
        name: 'TrueHD',
        matchType: 'normalizedContains',
        pattern: 'TrueHD',
        score: 50,
        mutexGroup: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'DTS-HD MA',
        matchType: 'normalizedContains',
        pattern: 'DTS-HD',
        score: 50,
        mutexGroup: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'DDP',
        matchType: 'normalizedContains',
        pattern: 'DDP',
        score: 15,
        mutexGroup: 'Lossy Codec',
        enabled: true
      },
      {
        name: 'DD',
        matchType: 'normalizedContains',
        pattern: 'DD',
        score: 10,
        mutexGroup: 'Lossy Codec',
        enabled: true
      }
    ],
    mutexGroups: [
      {
        name: 'Channel Layout',
        enabled: true
      },
      {
        name: 'Lossless Codec',
        enabled: true
      },
      {
        name: 'Lossy Codec',
        enabled: true
      }
    ]
  };
}

function createDefaultLanguagePreference(audioScoreProfiles, languages) {
  const languageTagOptions = createLanguageTagOptions(languages);
  const languageTag = getDefaultLanguageTag(languageTagOptions);
  const entries = [
    {
      languageTag,
      enabled: true
    }
  ];

  if (languageTag.toLowerCase() !== ORIGIN_LANGUAGE_TAG.toLowerCase()) {
    entries.push({
      languageTag: ORIGIN_LANGUAGE_TAG,
      enabled: true
    });
  }

  return {
    id: 0,
    name: translate('DefaultAudioLanguagePreferenceName'),
    enabled: true,
    scoreGapThreshold: 35,
    audioScoreProfileId: audioScoreProfiles[0]?.id || null,
    entries
  };
}

function cloneLanguageMapping(item) {
  return {
    ...item,
    language: item.language ? { ...item.language } : null,
    aliases: [...(item.aliases || [])]
  };
}

function cloneAudioScoreProfile(item) {
  return {
    ...item,
    rules: (item.rules || []).map((rule) => ({ ...rule })),
    mutexGroups: (item.mutexGroups || []).map((group) => ({ ...group }))
  };
}

function cloneLanguagePreference(item) {
  return {
    ...item,
    entries: (item.entries || []).map((entry) => ({ ...entry }))
  };
}

function ConfigSection({ legend, children }) {
  return (
    <FieldSet legend={legend}>
      {children}
    </FieldSet>
  );
}

function EmptyState({ children }) {
  return <div className={styles.emptyState}>{children}</div>;
}

function ActionButtons({ onEditPress, onDeletePress }) {
  return (
    <div className={styles.actions}>
      <IconButton
        aria-label={translate('Edit')}
        title={translate('Edit')}
        name={icons.EDIT}
        size={14}
        onPress={onEditPress}
      />

      <IconButton
        aria-label={translate('Delete')}
        title={translate('Delete')}
        name={icons.DELETE}
        size={14}
        onPress={onDeletePress}
      />
    </div>
  );
}

function AddCard({ onPress }) {
  return (
    <Card
      className={styles.addCard}
      onPress={onPress}
    >
      <div className={styles.center}>
        <Icon
          name={icons.ADD}
          size={45}
        />
      </div>
    </Card>
  );
}

function ConfigCard({ title, labels, enabled, onEditPress, onDeletePress }) {
  return (
    <Card
      className={styles.configCard}
      overlayContent={true}
      onPress={onEditPress}
    >
      <div className={styles.cardTitleContainer}>
        <div className={styles.cardTitle}>
          {title}
        </div>

        <ActionButtons
          onEditPress={onEditPress}
          onDeletePress={onDeletePress}
        />
      </div>

      <div className={styles.labels}>
        {(labels || []).filter(Boolean).map((label, index) => (
          <Label
            key={`${label}-${index}`}
            className={styles.label}
          >
            {label}
          </Label>
        ))}

        {enabled ? null : (
          <Label className={styles.label} kind={kinds.WARNING}>
            {translate('Disabled')}
          </Label>
        )}
      </div>
    </Card>
  );
}

function TextField({ label, value, placeholder, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <input
        className={styles.input}
        type="text"
        value={value || ''}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function NumberField({ label, value, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <input
        className={styles.input}
        type="number"
        value={value ?? 0}
        onChange={(event) => {
          const nextValue = Number.parseInt(event.target.value);
          onChange(Number.isNaN(nextValue) ? 0 : nextValue);
        }}
      />
    </label>
  );
}

function SelectField({ label, value, children, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <select
        className={styles.input}
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value)}
      >
        {children}
      </select>
    </label>
  );
}

function CheckboxField({ label, checked, onChange }) {
  return (
    <label className={styles.checkboxField}>
      <input
        type="checkbox"
        checked={!!checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span>{label}</span>
    </label>
  );
}

function getAudioScoreRuleName(rule, index) {
  return rule.name || rule.pattern || `${translate('AudioScoreRules')} ${index + 1}`;
}

function LanguageMappingForm({ draft, languages, setDraft }) {
  const aliases = draft.aliases || [];

  return (
    <>
      <div className={styles.formGrid}>
        <SelectField
          label={translate('Language')}
          value={draft.language?.id ?? ''}
          onChange={(value) => {
            const language = languages.find((item) => item.id === Number(value));
            setDraft((current) => ({
              ...current,
              language: language || current.language
            }));
          }}
        >
          {languages.map((language) => (
            <option key={language.id} value={language.id}>
              {language.name}
            </option>
          ))}
        </SelectField>
      </div>

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioLanguageAliases')}</h3>
        <Button
          size="small"
          onPress={() => {
            setDraft((current) => ({
              ...current,
              aliases: [...(current.aliases || []), '']
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.inputList}>
        {aliases.map((alias, index) => (
          <div key={index} className={styles.inputListItem}>
            <input
              className={styles.input}
              type="text"
              value={alias || ''}
              onChange={(event) => {
                setDraft((current) => {
                  const nextAliases = [...(current.aliases || [])];
                  nextAliases[index] = event.target.value;

                  return {
                    ...current,
                    aliases: nextAliases
                  };
                });
              }}
            />

            <IconButton
              aria-label={translate('Delete')}
              title={translate('Delete')}
              name={icons.DELETE}
              size={12}
              onPress={() => {
                setDraft((current) => ({
                  ...current,
                  aliases: (current.aliases || []).filter((_, aliasIndex) => aliasIndex !== index)
                }));
              }}
            />
          </div>
        ))}
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />
    </>
  );
}

function AudioScoreMutexGroupModal({
  editor,
  rules,
  setEditor,
  onSavePress,
  onDeletePress,
  onModalClose
}) {
  if (!editor) {
    return null;
  }

  const selectedRuleIndexes = editor.selectedRuleIndexes || [];
  const selectedRuleIndexSet = new Set(selectedRuleIndexes);
  const availableRuleIndexes = rules
    .map((_, index) => index)
    .filter((index) => !selectedRuleIndexSet.has(index));

  function updateGroup(field, value) {
    setEditor((current) => ({
      ...current,
      group: {
        ...current.group,
        [field]: value
      }
    }));
  }

  function addRule(ruleIndex) {
    setEditor((current) => ({
      ...current,
      isRulePickerOpen: false,
      selectedRuleIndexes: [
        ...(current.selectedRuleIndexes || []),
        ruleIndex
      ]
    }));
  }

  function removeRule(ruleIndex) {
    setEditor((current) => ({
      ...current,
      selectedRuleIndexes: (current.selectedRuleIndexes || [])
        .filter((index) => index !== ruleIndex)
    }));
  }

  return (
    <Modal
      isOpen={true}
      size="large"
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {
            editor.index == null ?
              translate('AddAudioScoreMutexGroup') :
              translate('EditAudioScoreMutexGroup')
          }
        </ModalHeader>

        <ModalBody>
          <div className={styles.formGrid}>
            <TextField
              label={translate('Name')}
              value={editor.group.name}
              onChange={(value) => updateGroup('name', value)}
            />
          </div>

          <CheckboxField
            label={translate('Enabled')}
            checked={editor.group.enabled}
            onChange={(value) => updateGroup('enabled', value)}
          />

          <div className={styles.arrayHeader}>
            <h3>{translate('AudioScoreRules')}</h3>
          </div>

          <div className={styles.cardGrid}>
            {selectedRuleIndexes.map((ruleIndex) => {
              const rule = rules[ruleIndex];

              if (!rule) {
                return null;
              }

              return (
                <Card
                  key={ruleIndex}
                  className={styles.configCard}
                >
                  <div className={styles.cardTitleContainer}>
                    <div className={styles.cardTitle}>
                      {getAudioScoreRuleName(rule, ruleIndex)}
                    </div>

                    <IconButton
                      aria-label={translate('Remove')}
                      title={translate('Remove')}
                      name={icons.DELETE}
                      size={12}
                      onPress={() => removeRule(ruleIndex)}
                    />
                  </div>

                  <div className={styles.labels}>
                    <Label className={styles.label}>{rule.pattern}</Label>
                    <Label className={styles.label}>{rule.score ?? 0}</Label>
                  </div>
                </Card>
              );
            })}

            <AddCard
              onPress={() => {
                setEditor((current) => ({
                  ...current,
                  isRulePickerOpen: true
                }));
              }}
            />
          </div>

          {editor.isRulePickerOpen ? (
            <div className={styles.pickerPanel}>
              <div className={styles.arrayHeader}>
                <h3>{translate('AvailableAudioScoreRules')}</h3>
              </div>

              {availableRuleIndexes.length ? (
                <div className={styles.cardGrid}>
                  {availableRuleIndexes.map((ruleIndex) => {
                    const rule = rules[ruleIndex];

                    return (
                      <Card
                        key={ruleIndex}
                        className={styles.configCard}
                        overlayContent={true}
                        onPress={() => addRule(ruleIndex)}
                      >
                        <div className={styles.cardTitle}>
                          {getAudioScoreRuleName(rule, ruleIndex)}
                        </div>

                        <div className={styles.labels}>
                          <Label className={styles.label}>{rule.pattern}</Label>
                          <Label className={styles.label}>{rule.score ?? 0}</Label>
                        </div>
                      </Card>
                    );
                  })}
                </div>
              ) : (
                <EmptyState>{translate('NoAudioScoreRulesAvailable')}</EmptyState>
              )}
            </div>
          ) : null}
        </ModalBody>

        <ModalFooter>
          <div className={styles.leftButtons}>
            {
              editor.index == null ? null : (
                <Button
                  kind="danger"
                  onPress={onDeletePress}
                >
                  {translate('Delete')}
                </Button>
              )
            }
          </div>

          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            kind="primary"
            isSpinning={false}
            onPress={onSavePress}
          >
            {translate('Save')}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

function AudioScoreProfileForm({ draft, setDraft }) {
  const mutexGroups = draft.mutexGroups || [];
  const rules = draft.rules || [];
  const [mutexGroupEditor, setMutexGroupEditor] = useState(null);
  const mutexGroupNames = Array.from(new Set(
    mutexGroups
      .map((group) => `${group.name || ''}`.trim())
      .filter(Boolean)
  ));

  function updateRule(index, field, value) {
    setDraft((current) => {
      const nextRules = [...(current.rules || [])];
      nextRules[index] = {
        ...nextRules[index],
        [field]: value
      };

      return {
        ...current,
        rules: nextRules
      };
    });
  }

  function openMutexGroupEditor(index) {
    const group = mutexGroups[index];
    const groupName = `${group?.name || ''}`.trim();
    const selectedRuleIndexes = rules
      .map((rule, ruleIndex) => (
        rule.mutexGroup === groupName ? ruleIndex : null
      ))
      .filter((ruleIndex) => ruleIndex != null);

    setMutexGroupEditor({
      index,
      originalName: groupName,
      group: {
        ...group
      },
      selectedRuleIndexes,
      isRulePickerOpen: false
    });
  }

  function openAddMutexGroupEditor() {
    setMutexGroupEditor({
      index: null,
      originalName: '',
      group: {
        name: '',
        enabled: true
      },
      selectedRuleIndexes: [],
      isRulePickerOpen: false
    });
  }

  function saveMutexGroupEditor() {
    if (!mutexGroupEditor) {
      return;
    }

    const nextName = `${mutexGroupEditor.group.name || ''}`.trim();
    const selectedRuleIndexes = new Set(mutexGroupEditor.selectedRuleIndexes || []);

    setDraft((current) => {
      const nextGroups = [...(current.mutexGroups || [])];
      const nextGroup = {
        ...mutexGroupEditor.group,
        name: nextName
      };

      if (mutexGroupEditor.index == null) {
        nextGroups.push(nextGroup);
      } else {
        nextGroups[mutexGroupEditor.index] = nextGroup;
      }

      const nextRules = (current.rules || []).map((rule, ruleIndex) => {
        if (selectedRuleIndexes.has(ruleIndex)) {
          return {
            ...rule,
            mutexGroup: nextName
          };
        }

        if (mutexGroupEditor.originalName && rule.mutexGroup === mutexGroupEditor.originalName) {
          return {
            ...rule,
            mutexGroup: ''
          };
        }

        return rule;
      });

      return {
        ...current,
        mutexGroups: nextGroups,
        rules: nextRules
      };
    });

    setMutexGroupEditor(null);
  }

  function deleteMutexGroupEditor() {
    if (!mutexGroupEditor || mutexGroupEditor.index == null) {
      return;
    }

    setDraft((current) => ({
      ...current,
      mutexGroups: (current.mutexGroups || [])
        .filter((_, groupIndex) => groupIndex !== mutexGroupEditor.index),
      rules: (current.rules || []).map((rule) => {
        if (mutexGroupEditor.originalName && rule.mutexGroup === mutexGroupEditor.originalName) {
          return {
            ...rule,
            mutexGroup: ''
          };
        }

        return rule;
      })
    }));

    setMutexGroupEditor(null);
  }

  return (
    <>
      <div className={styles.formGrid}>
        <TextField
          label={translate('Name')}
          value={draft.name}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              name: value
            }));
          }}
        />
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioScoreRules')}</h3>
        <Button
          size="small"
          onPress={() => {
            setDraft((current) => ({
              ...current,
              rules: [
                ...(current.rules || []),
                {
                  name: '',
                  matchType: 'contains',
                  pattern: '',
                  score: 0,
                  mutexGroup: '',
                  enabled: true
                }
              ]
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.formTableScroller}>
        <table className={styles.formTable}>
          <thead>
            <tr>
              <th>{translate('Name')}</th>
              <th>{translate('MatchType')}</th>
              <th>{translate('Pattern')}</th>
              <th>{translate('Score')}</th>
              <th>{translate('MutexGroup')}</th>
              <th>{translate('Enabled')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rules.map((rule, index) => (
              <tr key={index}>
                <td>
                  <input
                    className={styles.compactInput}
                    type="text"
                    value={rule.name || ''}
                    onChange={(event) => updateRule(index, 'name', event.target.value)}
                  />
                </td>
                <td>
                  <select
                    className={styles.compactInput}
                    value={rule.matchType || 'contains'}
                    onChange={(event) => updateRule(index, 'matchType', event.target.value)}
                  >
                    {MATCH_TYPES.map((matchType) => (
                      <option key={matchType.key} value={matchType.key}>
                        {matchType.value}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <input
                    className={styles.compactInput}
                    type="text"
                    value={rule.pattern || ''}
                    onChange={(event) => updateRule(index, 'pattern', event.target.value)}
                  />
                </td>
                <td>
                  <input
                    className={styles.scoreInput}
                    type="number"
                    value={rule.score ?? 0}
                    onChange={(event) => {
                      const value = Number.parseInt(event.target.value);
                      updateRule(index, 'score', Number.isNaN(value) ? 0 : value);
                    }}
                  />
                </td>
                <td>
                  <select
                    className={styles.compactInput}
                    value={rule.mutexGroup || ''}
                    onChange={(event) => updateRule(index, 'mutexGroup', event.target.value)}
                  >
                    <option value="">{translate('None')}</option>
                    {
                      [
                        ...(
                          rule.mutexGroup && !mutexGroupNames.includes(rule.mutexGroup) ?
                            [rule.mutexGroup] :
                            []
                        ),
                        ...mutexGroupNames
                      ].map((groupName) => (
                        <option key={groupName} value={groupName}>
                          {groupName}
                        </option>
                      ))
                    }
                  </select>
                </td>
                <td className={styles.checkboxCell}>
                  <input
                    type="checkbox"
                    checked={!!rule.enabled}
                    onChange={(event) => updateRule(index, 'enabled', event.target.checked)}
                  />
                </td>
                <td className={styles.rowButtonCell}>
                  <IconButton
                    aria-label={translate('Delete')}
                    title={translate('Delete')}
                    name={icons.DELETE}
                    size={12}
                    onPress={() => {
                      setDraft((current) => ({
                        ...current,
                        rules: (current.rules || []).filter((_, ruleIndex) => ruleIndex !== index)
                      }));
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioScoreMutexGroups')}</h3>
      </div>

      <div className={styles.cardGrid}>
        {mutexGroups.map((group, index) => (
          <Card
            key={index}
            className={styles.configCard}
            overlayContent={true}
            onPress={() => openMutexGroupEditor(index)}
          >
            <div className={styles.cardTitle}>
              {group.name || translate('Untitled')}
            </div>
          </Card>
        ))}

        {/* eslint-disable-next-line react/jsx-no-bind */}
        <AddCard onPress={openAddMutexGroupEditor} />
      </div>

      {/* eslint-disable react/jsx-no-bind */}
      <AudioScoreMutexGroupModal
        editor={mutexGroupEditor}
        rules={rules}
        setEditor={setMutexGroupEditor}
        onSavePress={saveMutexGroupEditor}
        onDeletePress={deleteMutexGroupEditor}
        onModalClose={() => setMutexGroupEditor(null)}
      />
      {/* eslint-enable react/jsx-no-bind */}
    </>
  );
}

function LanguagePreferenceForm({ draft, languages, audioScoreProfiles, setDraft }) {
  const entries = useMemo(() => draft.entries || [], [draft.entries]);
  const languageTagOptions = useMemo(() => createLanguageTagOptions(languages, entries), [languages, entries]);

  function updateEntry(index, field, value) {
    setDraft((current) => {
      const nextEntries = [...(current.entries || [])];
      nextEntries[index] = {
        ...nextEntries[index],
        [field]: value
      };

      return {
        ...current,
        entries: nextEntries
      };
    });
  }

  return (
    <>
      <div className={styles.formGrid}>
        <TextField
          label={translate('Name')}
          value={draft.name}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              name: value
            }));
          }}
        />

        <NumberField
          label={translate('ScoreGapThreshold')}
          value={draft.scoreGapThreshold}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              scoreGapThreshold: value
            }));
          }}
        />

        <SelectField
          label={translate('AudioScoreProfile')}
          value={draft.audioScoreProfileId || ''}
          onChange={(value) => {
            setDraft((current) => ({
              ...current,
              audioScoreProfileId: value ? Number(value) : null
            }));
          }}
        >
          <option value="">{translate('None')}</option>
          {audioScoreProfiles.map((profile) => (
            <option key={profile.id} value={profile.id}>
              {profile.name}
            </option>
          ))}
        </SelectField>
      </div>

      <CheckboxField
        label={translate('Enabled')}
        checked={draft.enabled}
        onChange={(value) => {
          setDraft((current) => ({
            ...current,
            enabled: value
          }));
        }}
      />

      <div className={styles.arrayHeader}>
        <h3>{translate('AudioLanguagePreferenceEntries')}</h3>
        <Button
          size="small"
          onPress={() => {
            const languageTag = getDefaultLanguageTag(languageTagOptions);

            setDraft((current) => ({
              ...current,
              entries: [
                ...(current.entries || []),
                {
                  languageTag,
                  enabled: true
                }
              ]
            }));
          }}
        >
          {translate('Add')}
        </Button>
      </div>

      <div className={styles.formTableScroller}>
        <table className={styles.formTable}>
          <thead>
            <tr>
              <th>{translate('LanguageTag')}</th>
              <th>{translate('Enabled')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {entries.map((entry, index) => (
              <tr key={index}>
                <td>
                  <select
                    className={styles.compactInput}
                    value={entry.languageTag || ''}
                    onChange={(event) => updateEntry(index, 'languageTag', event.target.value)}
                  >
                    <option value="">{translate('SelectLanguage')}</option>
                    {languageTagOptions.map((option) => (
                      <option key={option.key} value={option.value}>
                        {option.value}
                      </option>
                    ))}
                  </select>
                </td>
                <td className={styles.checkboxCell}>
                  <input
                    type="checkbox"
                    checked={!!entry.enabled}
                    onChange={(event) => updateEntry(index, 'enabled', event.target.checked)}
                  />
                </td>
                <td className={styles.rowButtonCell}>
                  <IconButton
                    aria-label={translate('Delete')}
                    title={translate('Delete')}
                    name={icons.DELETE}
                    size={12}
                    onPress={() => {
                      setDraft((current) => ({
                        ...current,
                        entries: (current.entries || []).filter((_, entryIndex) => entryIndex !== index)
                      }));
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function EditConfigModal({
  type,
  draft,
  languages,
  audioScoreProfiles,
  isSaving,
  saveError,
  setDraft,
  onSavePress,
  onModalClose
}) {
  if (!type || !draft) {
    return null;
  }

  let title = '';
  let body = null;

  if (type === 'languageMapping') {
    title = draft.id ? translate('EditAudioLanguageMapping') : translate('AddAudioLanguageMapping');
    body = (
      <LanguageMappingForm
        draft={draft}
        languages={languages}
        setDraft={setDraft}
      />
    );
  } else if (type === 'audioScoreProfile') {
    title = draft.id ? translate('EditAudioScoreProfile') : translate('AddAudioScoreProfile');
    body = (
      <AudioScoreProfileForm
        draft={draft}
        setDraft={setDraft}
      />
    );
  } else if (type === 'languagePreference') {
    title = draft.id ? translate('EditAudioLanguagePreference') : translate('AddAudioLanguagePreference');
    body = (
      <LanguagePreferenceForm
        draft={draft}
        languages={languages}
        audioScoreProfiles={audioScoreProfiles}
        setDraft={setDraft}
      />
    );
  }

  return (
    <Modal isOpen={true} size="large"
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{title}</ModalHeader>

        <ModalBody>
          {saveError ? <Alert kind="danger">{saveError}</Alert> : null}
          {body}
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            kind="primary"
            isSpinning={isSaving}
            onPress={onSavePress}
          >
            {translate('Save')}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

function ReleaseScoringSettingsPage() {
  const [isFetching, setIsFetching] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [languages, setLanguages] = useState([]);
  const [languageMappings, setLanguageMappings] = useState([]);
  const [audioScoreProfiles, setAudioScoreProfiles] = useState([]);
  const [languagePreferences, setLanguagePreferences] = useState([]);
  const [modalType, setModalType] = useState(null);
  const [draft, setDraft] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const audioScoreProfileNames = useMemo(() => {
    return audioScoreProfiles.reduce((acc, profile) => {
      acc[profile.id] = profile.name;
      return acc;
    }, {});
  }, [audioScoreProfiles]);

  const loadData = useCallback(async() => {
    setIsFetching(true);
    setLoadError(null);

    try {
      const [
        languageItems,
        mappingItems,
        audioScoreItems,
        preferenceItems
      ] = await Promise.all([
        requestJson({ url: '/language' }),
        requestJson({ url: ENDPOINTS.languageMapping }),
        requestJson({ url: ENDPOINTS.audioScoreProfile }),
        requestJson({ url: ENDPOINTS.languagePreference })
      ]);

      setLanguages(languageItems || []);
      setLanguageMappings(mappingItems || []);
      setAudioScoreProfiles(audioScoreItems || []);
      setLanguagePreferences(preferenceItems || []);
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('AudioFormatsLoadError')));
    } finally {
      setIsFetching(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  function openModal(type, item) {
    setSaveError(null);
    setModalType(type);

    if (item) {
      if (type === 'languageMapping') {
        setDraft(cloneLanguageMapping(item));
      } else if (type === 'audioScoreProfile') {
        setDraft(cloneAudioScoreProfile(item));
      } else if (type === 'languagePreference') {
        setDraft(cloneLanguagePreference(item));
      }

      return;
    }

    if (type === 'languageMapping') {
      setDraft(createDefaultLanguageMapping(languages));
    } else if (type === 'audioScoreProfile') {
      setDraft(createDefaultAudioScoreProfile());
    } else if (type === 'languagePreference') {
      setDraft(createDefaultLanguagePreference(audioScoreProfiles, languages));
    }
  }

  const closeModal = useCallback(() => {
    if (isSaving) {
      return;
    }

    setModalType(null);
    setDraft(null);
    setSaveError(null);
  }, [isSaving]);

  const saveDraft = useCallback(async() => {
    if (!modalType || !draft) {
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    try {
      const endpoint = ENDPOINTS[modalType];
      const id = draft.id;
      const method = id ? 'PUT' : 'POST';
      const url = id ? `${endpoint}/${id}` : endpoint;
      const data = modalType === 'languageMapping' ? {
        ...draft,
        aliases: (draft.aliases || [])
          .map((alias) => `${alias || ''}`.trim())
          .filter(Boolean)
      } : draft;

      await requestJson({
        url,
        method,
        data
      });

      setModalType(null);
      setDraft(null);
      await loadData();
    } catch (error) {
      setSaveError(getErrorMessage(error, translate('AudioFormatsSaveError')));
    } finally {
      setIsSaving(false);
    }
  }, [draft, loadData, modalType]);

  const confirmDelete = useCallback(async() => {
    if (!deleteTarget) {
      return;
    }

    setIsDeleting(true);

    try {
      await requestJson({
        url: `${ENDPOINTS[deleteTarget.type]}/${deleteTarget.item.id}`,
        method: 'DELETE'
      });

      setDeleteTarget(null);
      await loadData();
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('AudioFormatsDeleteError')));
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, loadData]);

  const cancelDelete = useCallback(() => {
    setDeleteTarget(null);
  }, []);

  const deleteName = deleteTarget?.item.name ||
    deleteTarget?.item.language?.name ||
    translate('SelectedItem');

  return (
    <PageContent title={translate('AudioFormatsSettings')}>
      <SettingsToolbar showSave={false} hasPendingChanges={false} />

      <PageContentBody>
        {loadError ? <Alert kind="danger">{loadError}</Alert> : null}

        {isFetching ? (
          <LoadingIndicator />
        ) : (
          <>
            <ConfigSection
              legend={translate('AudioLanguageMappings')}
            >
              <div className={styles.cardGrid}>
                {languageMappings.map((mapping) => (
                  <ConfigCard
                    key={mapping.id}
                    title={getLanguageName(mapping.language)}
                    labels={mapping.aliases || []}
                    enabled={mapping.enabled}
                    onEditPress={() => openModal('languageMapping', mapping)}
                    onDeletePress={() => setDeleteTarget({ type: 'languageMapping', item: mapping })}
                  />
                ))}

                <AddCard onPress={() => openModal('languageMapping')} />
              </div>
            </ConfigSection>

            <ConfigSection
              legend={translate('AudioScoreProfiles')}
            >
              <div className={styles.cardGrid}>
                {audioScoreProfiles.map((profile) => (
                  <ConfigCard
                    key={profile.id}
                    title={profile.name}
                    labels={[
                      `${(profile.rules || []).length} ${translate('AudioScoreRules')}`,
                      ...(profile.mutexGroups || []).map((group) => group.name)
                    ]}
                    enabled={profile.enabled}
                    onEditPress={() => openModal('audioScoreProfile', profile)}
                    onDeletePress={() => setDeleteTarget({ type: 'audioScoreProfile', item: profile })}
                  />
                ))}

                <AddCard onPress={() => openModal('audioScoreProfile')} />
              </div>
            </ConfigSection>

            <ConfigSection
              legend={translate('AudioLanguagePreferences')}
            >
              <div className={styles.cardGrid}>
                {languagePreferences.map((preference) => (
                  <ConfigCard
                    key={preference.id}
                    title={preference.name}
                    labels={[
                      ...(preference.entries || []).map((entry) => entry.languageTag),
                      `${translate('ScoreGapThreshold')}: ${preference.scoreGapThreshold}`,
                      audioScoreProfileNames[preference.audioScoreProfileId] || translate('None')
                    ]}
                    enabled={preference.enabled}
                    onEditPress={() => openModal('languagePreference', preference)}
                    onDeletePress={() => setDeleteTarget({ type: 'languagePreference', item: preference })}
                  />
                ))}

                <AddCard onPress={() => openModal('languagePreference')} />
              </div>
            </ConfigSection>

          </>
        )}

        <EditConfigModal
          type={modalType}
          draft={draft}
          languages={languages}
          audioScoreProfiles={audioScoreProfiles}
          isSaving={isSaving}
          saveError={saveError}
          setDraft={setDraft}
          onSavePress={saveDraft}
          onModalClose={closeModal}
        />

        <ConfirmModal
          isOpen={!!deleteTarget}
          kind="danger"
          title={translate('Delete')}
          message={translate('DeleteAudioFormatsConfigMessage', { name: deleteName })}
          confirmLabel={translate('Delete')}
          cancelLabel={translate('Cancel')}
          isSpinning={isDeleting}
          onConfirm={confirmDelete}
          onCancel={cancelDelete}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default ReleaseScoringSettingsPage;
